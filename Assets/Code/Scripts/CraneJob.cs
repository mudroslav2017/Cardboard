using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class CraneJob : MonoBehaviour
    {
        [Header("Access")]
        [SerializeField] Transform passage, westBoundary;
        [SerializeField] Transform ladderBottom, ladderTop, cabLanding, operatorStand;
        [SerializeField] FactoryInteractable lowerLadder, upperLadder, console;
        [SerializeField] TextMesh consoleStatus, consoleCount, consoleIntegrity;
        [SerializeField, Min(.1f)] float climbSpeed = 2.5f;

        [Header("Crane")]
        [SerializeField] Transform carriage, railStart, railEnd, boom, boomTip, hook, cable, guide;
        [SerializeField, Min(.1f)] float railSpeed = 2.8f;
        [SerializeField, Min(.1f)] float slewSpeed = 45f;
        [SerializeField, Min(.1f)] float hoistSpeed = 2f;
        [SerializeField] float minimumHookHeight = 1.18f;

        [Header("Loading bay")]
        [SerializeField] CranePallet pallet;
        [SerializeField] Transform palletSpawn;
        [SerializeField] Rigidbody truck;
        [SerializeField] BoxCollider loadingZone;
        [SerializeField] Transform truckStop, truckEntry, truckExit;
        [SerializeField] CraneDamageSurface truckDamage, wallDamage;

        enum PlayerMode { Ground, Ladder, Cabin, Operating }
        enum TruckState { Ready, Loaded, Leaving, Arriving }

        static readonly Color[] CabPaints =
        {
            new Color(.52f, .43f, .28f),
            new Color(.42f, .48f, .38f),
            new Color(.54f, .34f, .24f)
        };

        readonly RaycastHit[] guideHits = new RaycastHit[12];
        PlayerMode playerMode;
        CharacterController player;
        bool attached, toggleHook, resetPallet, waitingForUseRelease;
        float railInput, slewInput, hoistInput, precision = 1f;
        float railPosition, boomYaw, hookHeight;
        float settledTime, departureDelay;
        int deliveries;
        TruckState truckState;
        Vector3 carriedOnTruck;

        public bool HoldsPlayer => playerMode == PlayerMode.Ladder || playerMode == PlayerMode.Operating;
        public bool OnCabFloor => playerMode == PlayerMode.Cabin;
        public float CabFloor => cabLanding.position.y;
        public float BayWest => westBoundary.position.x;
        float LowestHook => railStart.position.y + minimumHookHeight;

        public bool AllowsBayAccess(Vector3 position) =>
            position.x < CardboardFactoryWorld.WorkshopWest + 1f &&
            (position.x < CardboardFactoryWorld.WorkshopWest || Mathf.Abs(passage.position.z - position.z) < 1.55f);

        void Awake()
        {
            railPosition = carriage.position.x;
            boomYaw = boom.eulerAngles.y;
            hookHeight = hook.position.y;
            guide.gameObject.SetActive(false);
            UpdateCrane();
        }

        void Update()
        {
            var game = CardboardFactoryGame.Instance;
            if (!game || game.PlayerBlocked || game.CaptureActive || !Application.isFocused ||
                Cursor.lockState != CursorLockMode.Locked || Keyboard.current == null)
            {
                railInput = slewInput = hoistInput = 0;
                toggleHook = resetPallet = false;
                if (game && (game.Ended || game.CaptureActive) && playerMode != PlayerMode.Ground)
                {
                    playerMode = PlayerMode.Ground;
                    player.enabled = true;
                    guide.gameObject.SetActive(false);
                }
            }
            else
                ReadControls(Keyboard.current, Time.deltaTime);

            UpdateConsole();
        }

        public void Use(FactoryInteractable target)
        {
            var runner = CardboardRunner.Player;
            if (!runner || HoldsPlayer) return;
            player = runner.GetComponent<CharacterController>();

            if (playerMode == PlayerMode.Cabin && target == console)
            {
                playerMode = PlayerMode.Operating;
                waitingForUseRelease = true;
                MovePlayerTo(operatorStand.position, false);
                guide.gameObject.SetActive(true);
            }
            else if (playerMode == PlayerMode.Cabin && target == upperLadder)
            {
                playerMode = PlayerMode.Ladder;
                MovePlayerTo(ladderTop.position, false);
            }
            else if (playerMode == PlayerMode.Ground && target == lowerLadder)
            {
                playerMode = PlayerMode.Ladder;
                MovePlayerTo(ladderBottom.position, false);
            }
        }

        void ReadControls(Keyboard keyboard, float dt)
        {
            if (playerMode == PlayerMode.Ladder)
            {
                Climb(Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed) * climbSpeed * dt);
                return;
            }

            if (playerMode != PlayerMode.Operating) return;
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                LeaveControls();
                return;
            }

            precision = keyboard.leftShiftKey.isPressed ? .28f : 1f;
            railInput = Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            if (!keyboard.eKey.isPressed) waitingForUseRelease = false;
            slewInput = Axis(keyboard.qKey.isPressed, keyboard.eKey.isPressed && !waitingForUseRelease);
            hoistInput = Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            toggleHook |= keyboard.spaceKey.wasPressedThisFrame;
            resetPallet |= keyboard.rKey.wasPressedThisFrame;
        }

        void LeaveControls()
        {
            playerMode = PlayerMode.Cabin;
            railInput = slewInput = hoistInput = 0;
            toggleHook = resetPallet = false;
            player.enabled = true;
            guide.gameObject.SetActive(false);
        }

        void Climb(float step)
        {
            if (Mathf.Abs(step) < .0001f) return;

            Vector3 feet = player.transform.position;
            feet.y = Mathf.Clamp(feet.y + step, ladderBottom.position.y, ladderTop.position.y);
            player.transform.position = feet;
            if (step > 0f && feet.y >= ladderTop.position.y)
            {
                playerMode = PlayerMode.Cabin;
                MovePlayerTo(cabLanding.position, true);
            }
            else if (step < 0f && feet.y <= ladderBottom.position.y)
            {
                playerMode = PlayerMode.Ground;
                MovePlayerTo(ladderBottom.position - ladderBottom.forward * .42f, true);
            }
        }

        void MovePlayerTo(Vector3 feet, bool allowWalking)
        {
            player.enabled = false;
            player.transform.position = feet;
            player.enabled = allowWalking;
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (playerMode == PlayerMode.Operating)
            {
                if (resetPallet && truckState == TruckState.Ready) ReturnPallet();
                if (toggleHook && truckState == TruckState.Ready) ToggleHook();
                DriveCrane(dt);
                UpdateGuide();
                player.transform.position = operatorStand.position;
            }

            resetPallet = toggleHook = false;
            AdvanceTruck(dt);
        }

        void DriveCrane(float dt)
        {
            // Apply axes separately so a blocked motion doesn't prevent lifting away from an obstacle.
            float height = Mathf.Clamp(hookHeight + hoistInput * hoistSpeed * precision * dt, LowestHook, boomTip.position.y - .58f);
            float position = Mathf.Clamp(railPosition + railInput * railSpeed * precision * dt, railStart.position.x, railEnd.position.x);
            float yaw = Mathf.Repeat(boomYaw + slewInput * slewSpeed * precision * dt, 360f);
            TryMove(railPosition, boomYaw, height, dt);
            TryMove(position, boomYaw, hookHeight, dt);
            TryMove(railPosition, yaw, hookHeight, dt);
            UpdateCrane();
        }

        void TryMove(float position, float yaw, float height, float dt)
        {
            if (attached)
            {
                Vector3 nextHook = HookAt(position, yaw, height);
                Vector3 displacement = nextHook - pallet.Eye;
                if (!pallet.CanMove(displacement, displacement.magnitude / dt)) return;
                pallet.PlaceUnder(nextHook);
            }

            railPosition = position;
            boomYaw = yaw;
            hookHeight = height;
        }

        Vector3 HookAt(float position, float yaw, float height)
        {
            Vector3 reach = Quaternion.Euler(0, yaw, 0) * boomTip.localPosition;
            return new Vector3(position + reach.x, height, carriage.position.z + reach.z);
        }

        void UpdateCrane()
        {
            carriage.position = new Vector3(railPosition, carriage.position.y, carriage.position.z);
            boom.rotation = Quaternion.Euler(0, boomYaw, 0);
            hook.position = HookAt(railPosition, boomYaw, hookHeight);

            Vector3 end = boomTip.position;
            cable.position = (end + hook.position) * .5f;
            cable.localScale = new Vector3(.05f, end.y - hook.position.y, .05f);

        }

        void UpdateGuide()
        {
            int count = Physics.RaycastNonAlloc(new Ray(new Vector3(hook.position.x, boomTip.position.y - .2f, hook.position.z),
                Vector3.down), guideHits, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = guideHits[i];
                if (hit.collider.gameObject == pallet.gameObject || hit.distance >= nearest) continue;
                nearest = hit.distance;
                guide.position = hit.point + Vector3.up * .018f;
            }
        }

        bool CanLatch() => truckState == TruckState.Ready && pallet.IsStill &&
            (hook.position - pallet.Eye).sqrMagnitude <= .36f * .36f;

        void ToggleHook()
        {
            if (attached)
            {
                attached = false;
                pallet.Release();
                return;
            }

            if (!CanLatch()) return;
            Vector3 hookPoint = hook.position;
            hookPoint.y = Mathf.Max(LowestHook, pallet.Eye.y);
            if (!pallet.CanMove(hookPoint - pallet.Eye, 0f)) return;

            hookHeight = hookPoint.y;
            pallet.Attach(hookPoint);
            attached = true;
        }

        void ReturnPallet()
        {
            attached = false;
            settledTime = 0f;
            pallet.ResetAt(palletSpawn);
        }

        bool PalletOnDeck()
        {
            Bounds slot = loadingZone.bounds;
            Bounds load = pallet.Footprint;
            return load.min.x >= slot.min.x && load.max.x <= slot.max.x &&
                load.min.z >= slot.min.z && load.max.z <= slot.max.z &&
                Mathf.Abs(load.min.y - slot.min.y) < .1f && pallet.IsStill;
        }

        void AdvanceTruck(float dt)
        {
            switch (truckState)
            {
                case TruckState.Ready:
                    if (pallet.Eye.y < -1f) ReturnPallet();
                    settledTime = !attached && PalletOnDeck() ? settledTime + dt : 0f;
                    if (settledTime < .4f) return;

                    pallet.Secure();
                    carriedOnTruck = pallet.Eye - truck.position;
                    deliveries++;
                    departureDelay = .65f;
                    truckState = TruckState.Loaded;
                    break;

                case TruckState.Loaded:
                    departureDelay -= dt;
                    if (departureDelay <= 0f) truckState = TruckState.Leaving;
                    break;

                case TruckState.Leaving:
                    if (!MoveTruck(truckExit.position, 8.5f, dt, true)) return;

                    pallet.gameObject.SetActive(false);
                    truck.position = truckEntry.position;
                    truckDamage.Restore(CabPaints[deliveries % CabPaints.Length]);
                    truckState = TruckState.Arriving;
                    break;

                case TruckState.Arriving:
                    if (!MoveTruck(truckStop.position, 8.5f, dt, false)) return;

                    pallet.gameObject.SetActive(true);
                    ReturnPallet();
                    truckState = TruckState.Ready;
                    break;
            }
        }

        bool MoveTruck(Vector3 destination, float speed, float dt, bool loaded)
        {
            truck.position = Vector3.MoveTowards(truck.position, destination, speed * dt);
            if (loaded) pallet.PlaceUnder(truck.position + carriedOnTruck);
            return truck.position == destination;
        }

        void UpdateConsole()
        {
            string state = truckState == TruckState.Arriving ? "TRUCK REVERSING" :
                truckState != TruckState.Ready ? "TRUCK DEPARTING" :
                attached ? "LOAD ON HOOK" : CanLatch() ? "HOOK IN POSITION" : "BAY READY";

            if (consoleStatus.text != state) consoleStatus.text = state;
            string count = $"LOADS {deliveries}";
            if (consoleCount.text != count) consoleCount.text = count;
            string integrity = $"TRK {truckDamage.Integrity}%  WALL {wallDamage.Integrity}%";
            if (consoleIntegrity.text != integrity) consoleIntegrity.text = integrity;
        }
    }
}
