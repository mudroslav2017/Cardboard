using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class FactoryCraneJob : MonoBehaviour
    {
        [Header("Factory access")]
        [SerializeField] CardboardFactoryGame game;
        [SerializeField] Transform entrance, workshopLanding, westBoundary, hallRecovery;

        [Header("Ladder, roof and cab")]
        [SerializeField] Transform ladderBottom, ladderTop, ladderExit, catwalkLanding;
        [SerializeField] Transform hatchBottom, hatchTop, cabinLanding, roofLanding, driverStand;
        [SerializeField] TextMesh boardStatus, boardCount;

        [Header("Rail crane")]
        [SerializeField] Transform chassis, slew, minimumRail, maximumRail, tip;
        [SerializeField] FactoryCraneCable hoistLine;
        [SerializeField] Rigidbody hook;
        [SerializeField, Min(.1f)] float railSpeed = 2.8f;
        [SerializeField, Min(.1f)] float slewSpeed = 57f;
        [SerializeField, Min(.1f)] float hoistSpeed = 1.8f;
        [SerializeField, Range(15f, 70f)] float spillAngle = 41f;

        [Header("Freight")]
        [SerializeField] FactoryCraneLoad load;
        [SerializeField] Transform loadHome;
        [SerializeField] Rigidbody truck;
        [SerializeField] Transform dock, tunnel;
        [SerializeField] BoxCollider trailerZone;
        [SerializeField] FactoryCraneImpact truckDamage, partitionDamage;
        [SerializeField] FactoryForklift forklift;

        enum TruckPhase { Parked, Dispatching, Departing, Reversing }
        static readonly Color[] TruckPaint =
        {
            new Color(.74f, .51f, .32f),
            new Color(.57f, .66f, .40f),
            new Color(.68f, .39f, .23f)
        };

        CardboardRunner operatorPlayer;
        CardboardCamera view;
        FactoryCraneGauge loadGauge;
        Transform climbBottom, climbTop, climbExitBelow, climbExitAbove;
        Vector3 hatchStart, hatchEyeOffset;
        Quaternion hatchStartRotation, hatchViewRotation;
        float hatchElapsed;
        bool enteringHatch;
        const float HatchDuration = 1.85f;
        const float OverloadHold = .16f;
        bool operating, waitForInteractRelease, releaseLoad, resetLoad, mustClearHook, attemptedLanding, landingFault;
        float railInput, slewInput, hoistInput, precision;
        float railVelocity, slewVelocity;
        float startRail, faultDelay, spillTime, latchTime, settleTime, recoverTime, dispatchTime;
        Quaternion startSlew;
        Vector3 carriedOnTruck;
        Vector2 loadDeflection;
        float swingRatio;
        TruckPhase truckPhase;
        int deliveries;
        bool coopReplica;

        public bool HatchInMotion { get; private set; }
        public bool HoldsPlayer => operating || climbBottom != null || HatchInMotion || (forklift && forklift.Driving);
        internal FactoryCraneLoad PickupPallet => truckPhase == TruckPhase.Parked && !attemptedLanding ? load : null;
        internal bool IsAvailable => isActiveAndEnabled && hoistLine;

        internal bool TryGetForkliftSeat(Transform player, out Vector3 position, out float yaw)
        {
            if (forklift) return forklift.TryGetDriverSeat(player, out position, out yaw);
            position = default;
            yaw = 0f;
            return false;
        }

        public bool InHall(Vector3 point) => point.x < entrance.position.x - .42f && point.y > -2.5f;

        public bool CanWalkThrough(Vector3 point) => InHall(point) ||
            (point.x < entrance.position.x + .90f && Mathf.Abs(point.z - entrance.position.z) < 1.38f);

        public float WestLimit => westBoundary.position.x;
        public Vector3 RecoveryPoint => hallRecovery.position;

        void Awake()
        {
            if (!hoistLine) hoistLine = GetComponentInChildren<FactoryCraneCable>(true);
            if (!hoistLine)
            {
                enabled = false;
                Debug.LogWarning("The crane hoist is missing from the loaded scene. Reopen FactoryShift to load the current scene asset.", this);
                return;
            }
            startRail = chassis.localPosition.x;
            startSlew = slew.localRotation;
        }

        public void ResetJob()
        {
            if (!IsAvailable) return;
            ReturnToWorkshop(CardboardRunner.Player);
            if (forklift) forklift.ResetVehicle();
            ClearMotorCommands();
            railVelocity = slewVelocity = 0f;
            Vector3 position = chassis.localPosition;
            position.x = startRail;
            chassis.localPosition = position;
            slew.localRotation = startSlew;
            truckPhase = TruckPhase.Parked;
            truck.position = dock.position;
            deliveries = 0;
            faultDelay = 0f;
            truckDamage.Restore(TruckPaint[0]);
            partitionDamage.Restore();
            RestoreLoad();
            hoistLine.ResetAt(tip.position, hoistLine.InitialLength);
        }

        public bool Use(CardboardRunner player, CraneAccess action)
        {
            if (!IsAvailable || !player || HoldsPlayer || player.InSewer || game.IntroActive || game.CaptureActive || game.Ended) return false;
            operatorPlayer = player;
            switch (action)
            {
                case CraneAccess.LadderBottom:
                    BeginClimb(ladderBottom, ladderTop, ladderExit, catwalkLanding, false);
                    break;
                case CraneAccess.LadderTop:
                    BeginClimb(ladderBottom, ladderTop, ladderExit, catwalkLanding, true);
                    break;
                case CraneAccess.RoofHatch:
                    BeginHatch(true);
                    break;
                case CraneAccess.CabinHatch:
                    BeginHatch(false);
                    break;
                case CraneAccess.Forklift:
                    return forklift && forklift.Enter(player);
                case CraneAccess.Console:
                    operating = true;
                    waitForInteractRelease = true;
                    player.PlaceAtWorkAnchor(driverStand.position, false);
                    view = player == CardboardRunner.Player && Camera.main ? Camera.main.GetComponent<CardboardCamera>() : null;
                    if (view)
                    {
                        view.transform.rotation = Quaternion.LookRotation(slew.forward - Vector3.up * .65f);
                        view.AdoptCurrentView();
                    }
                    break;
            }
            return true;
        }

        void BeginClimb(Transform bottom, Transform top, Transform below, Transform above, bool fromTop)
        {
            climbBottom = bottom;
            climbTop = top;
            climbExitBelow = below;
            climbExitAbove = above;
            operatorPlayer.PlaceAtWorkAnchor((fromTop ? top : bottom).position, false);
        }

        void Climb(float step)
        {
            if (Mathf.Abs(step) < .0001f) return;
            Vector3 feet = operatorPlayer.transform.position;
            feet.y = Mathf.Clamp(feet.y + step, climbBottom.position.y, climbTop.position.y);
            operatorPlayer.transform.position = feet;
            if (step > 0f && feet.y >= climbTop.position.y)
                EndClimb(climbExitAbove.position);
            else if (step < 0f && feet.y <= climbBottom.position.y)
                EndClimb(climbExitBelow.position);
        }

        void EndClimb(Vector3 feet)
        {
            climbBottom = null;
            operatorPlayer.PlaceAtWorkAnchor(feet, true);
        }

        void BeginHatch(bool entering)
        {
            view = operatorPlayer == CardboardRunner.Player && Camera.main ? Camera.main.GetComponent<CardboardCamera>() : null;
            enteringHatch = entering;
            hatchStart = operatorPlayer.transform.position;
            hatchStartRotation = view ? view.transform.rotation : Quaternion.LookRotation(slew.forward);
            hatchViewRotation = hatchStartRotation;
            hatchEyeOffset = Vector3.zero;
            hatchElapsed = 0f;
            HatchInMotion = true;
            operatorPlayer.PlaceAtWorkAnchor(hatchStart, false);
        }

        void AnimateHatch(float dt)
        {
            hatchElapsed = Mathf.Min(HatchDuration, hatchElapsed + dt);
            float t = hatchElapsed / HatchDuration;
            Vector3 entry = (enteringHatch ? hatchTop : hatchBottom).position;
            Vector3 endOfLadder = (enteringHatch ? hatchBottom : hatchTop).position;
            Vector3 landing = (enteringHatch ? cabinLanding : roofLanding).position;
            float stepOn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .25f, t));
            float climb = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, .77f, t));
            float stepOff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.77f, 1f, t));
            var feet = Vector3.Lerp(Vector3.Lerp(hatchStart, entry, stepOn), endOfLadder, climb);
            operatorPlayer.transform.position = Vector3.Lerp(feet, landing, stepOff);

            float grip = Mathf.Sin(Mathf.PI * t);
            float rung = Mathf.Sin(climb * 8f * Mathf.PI);
            hatchEyeOffset = hatchTop.right * (.035f * grip * rung) + Vector3.up * (grip * (-.18f + .04f * rung));
            var towardsLadder = Quaternion.LookRotation(-hatchTop.forward + Vector3.up * (enteringHatch ? -.48f : .24f));
            var exitFacing = enteringHatch ? slew.forward : Vector3.ProjectOnPlane(landing - endOfLadder, Vector3.up);
            var towardsExit = Quaternion.LookRotation(exitFacing);
            hatchViewRotation = Quaternion.Slerp(Quaternion.Slerp(hatchStartRotation, towardsLadder, stepOn), towardsExit, stepOff)
                * Quaternion.Euler(0f, 0f, 2.2f * grip * rung);

            if (t < 1f) return;
            HatchInMotion = false;
            operatorPlayer.PlaceAtWorkAnchor(landing, true);
            if (view)
            {
                view.transform.rotation = towardsExit;
                view.AdoptCurrentView();
            }
        }

        internal bool TryGetHatchView(out Vector3 position, out Quaternion rotation)
        {
            position = HatchInMotion ? operatorPlayer.transform.position + Vector3.up * 1.62f + hatchEyeOffset : default;
            rotation = hatchViewRotation;
            return HatchInMotion;
        }

        void LeaveControls()
        {
            operating = false;
            ClearMotorCommands();
            railVelocity = slewVelocity = 0f;
            operatorPlayer.PlaceAtWorkAnchor(driverStand.position, true);
        }

        public void ReturnToWorkshop(CardboardRunner player)
        {
            if (!player || (!HoldsPlayer && !InHall(player.transform.position))) return;
            StopOperator();
            player.PlaceAtWorkAnchor(workshopLanding.position, true);
        }

        internal void SpawnAtShop(CardboardRunner player, CardboardCamera camera)
        {
            if (game.production) game.production.CancelPlayer();
            StopOperator();
            player.LeaveSewer();
            player.PlaceAtWorkAnchor(RecoveryPoint, true);
            var towardsLadder = Vector3.ProjectOnPlane(ladderBottom.position - RecoveryPoint, Vector3.up);
            var facing = Quaternion.LookRotation(towardsLadder);
            player.transform.rotation = facing;
            camera.transform.SetPositionAndRotation(RecoveryPoint + Vector3.up * 1.62f, facing);
            camera.AdoptCurrentView();
        }

        void StopOperator()
        {
            if (forklift && forklift.Driving) forklift.LeaveSeat();
            operating = false;
            climbBottom = null;
            HatchInMotion = false;
            ClearMotorCommands();
            railVelocity = slewVelocity = 0f;
        }

        void Update()
        {
            if (game.MultiplayerPrototype) return;
            if (!HoldsPlayer) return;
            if (game.Ended || game.CaptureActive || game.EndingBusy)
            {
                ReturnToWorkshop(operatorPlayer);
                return;
            }
            if (forklift && forklift.Driving) return;

            if (game.PlayerBlocked || !Application.isFocused)
            {
                ClearMotorCommands();
                return;
            }

            if (HatchInMotion)
            {
                AnimateHatch(Time.deltaTime);
                return;
            }

            var keyboard = Keyboard.current;
            if (Cursor.lockState == CursorLockMode.Locked && keyboard != null) ReadControls(keyboard, Mouse.current, Time.deltaTime);
            else ClearMotorCommands();
        }

        void ReadControls(Keyboard keyboard, Mouse mouse, float dt)
        {
            if (climbBottom)
            {
                float forward = Axis(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
                Climb(forward * 2.5f * dt);
                return;
            }
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                LeaveControls();
                return;
            }

            if (!keyboard.eKey.isPressed) waitForInteractRelease = false;
            railInput = Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            slewInput = Axis(keyboard.qKey.isPressed, keyboard.eKey.isPressed && !waitForInteractRelease);
            hoistInput = mouse != null ? Axis(mouse.leftButton.isPressed, mouse.rightButton.isPressed) : 0f;
            precision = keyboard.leftShiftKey.isPressed ? .3f : 1f;
            releaseLoad |= keyboard.spaceKey.wasPressedThisFrame;
            resetLoad |= keyboard.rKey.wasPressedThisFrame;
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        void ClearMotorCommands()
        {
            railInput = slewInput = hoistInput = 0f;
            precision = 1f;
            releaseLoad = resetLoad = false;
        }

        void FixedUpdate()
        {
            if (!coopReplica) Advance(Time.fixedDeltaTime);
        }

        void Advance(float dt)
        {
            if (!IsAvailable) return;
            faultDelay = Mathf.Max(0f, faultDelay - dt);
            if (operating) MoveCrane(dt);
            float winding = operating ? hoistInput * hoistSpeed * precision : 0f;
            if (forklift && forklift.Driving && !load.Attached)
                winding = Mathf.Clamp((hoistLine.Length - 3f) / dt, 0f, 2.7f);
            hoistLine.Drive(tip.position, winding, load.Attached, dt);

            if (truckPhase == TruckPhase.Parked) UpdateLoad(dt);
            else UpdateTruck(dt);
            releaseLoad = resetLoad = false;
        }

        void MoveCrane(float dt)
        {
            var railAxis = maximumRail.position - minimumRail.position;
            float cabSide = Vector3.Dot(slew.right, railAxis) < 0f ? -1f : 1f;
            railVelocity = Mathf.MoveTowards(railVelocity, cabSide * railInput * railSpeed * precision, 6f * dt);
            slewVelocity = Mathf.MoveTowards(slewVelocity, slewInput * slewSpeed * precision, 103f * dt);
            Vector3 position = chassis.localPosition;
            float next = position.x + railVelocity * dt;
            position.x = Mathf.Clamp(next, minimumRail.localPosition.x, maximumRail.localPosition.x);
            if (next != position.x) railVelocity = 0f;
            chassis.localPosition = position;
            float turn = slewVelocity * dt;
            slew.localRotation *= Quaternion.Euler(0f, turn, 0f);
            if (view) view.RotateWithSupport(turn);
            operatorPlayer.transform.position = driverStand.position;
        }

        void LateUpdate()
        {
            if (coopReplica) return;
            string status = load.Spilled ? "REPLACING SPILLED LOAD" :
                truckPhase == TruckPhase.Reversing ? "TRUCK REVERSING" :
                truckPhase != TruckPhase.Parked ? "TRUCK DEPARTING" :
                load.Attached ? (OnTrailer() ? "ON DECK / SPACE" : "LOAD ON HOOK") : "HOOK READY";
            if (boardStatus.text != status) boardStatus.text = status;
            string count = $"LOADS {deliveries}  /  TRUCK {truckDamage.Condition}%";
            if (boardCount.text != count) boardCount.text = count;
            UpdateLoadGauge(Time.unscaledDeltaTime);
        }

        internal Transform[] PrepareCoopReplication(bool replica)
        {
            coopReplica = replica;
            var parts = new System.Collections.Generic.List<Transform>
            {
                chassis, slew, hook.transform, truck.transform, load.transform
            };
            hoistLine.AddCoopParts(parts);
            load.AddCoopParts(parts);
            if (forklift) forklift.AddCoopParts(parts);
            if (replica)
            {
                if (forklift) forklift.enabled = false;
                foreach (var item in parts)
                    if (item.TryGetComponent(out Rigidbody body))
                    {
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        body.isKinematic = true;
                    }
            }
            return parts.ToArray();
        }

        internal void TickCoop(FactoryCoopControls input, float dt)
        {
            if (!HoldsPlayer || coopReplica) return;
            if (game.Ended || game.CaptureActive)
            {
                ReturnToWorkshop(operatorPlayer);
                return;
            }
            float lateral = Axis((input & FactoryCoopControls.Left) != 0, (input & FactoryCoopControls.Right) != 0);
            float forward = Axis((input & FactoryCoopControls.Back) != 0, (input & FactoryCoopControls.Forward) != 0);
            float lift = Axis((input & FactoryCoopControls.Primary) != 0, (input & FactoryCoopControls.Secondary) != 0);
            if (forklift && forklift.Driving) forklift.Control(false, lateral, forward, lift);
            else if (HatchInMotion) AnimateHatch(dt);
            else if (climbBottom) Climb(forward * 2.5f * dt);
            else
            {
                bool interact = (input & FactoryCoopControls.E) != 0;
                if (!interact) waitForInteractRelease = false;
                railInput = lateral;
                slewInput = Axis((input & FactoryCoopControls.Q) != 0, interact && !waitForInteractRelease);
                hoistInput = lift;
                precision = (input & FactoryCoopControls.Precision) != 0 ? .3f : 1f;
            }
        }

        internal void CoopControl(FactoryCoopAction action)
        {
            if (forklift && forklift.Driving)
            {
                if (action == FactoryCoopAction.LeaveTool) forklift.Control(true, 0, 0, 0);
            }
            else if (operating)
            {
                if (action == FactoryCoopAction.LeaveTool) LeaveControls();
                if (action == FactoryCoopAction.ToolSpace) releaseLoad = true;
                if (action == FactoryCoopAction.ToolReset) resetLoad = true;
            }
        }

        internal void ReadCoopBoard(FactoryCoopCraneState state)
        {
            state.Status = boardStatus.text;
            state.Counter = boardCount.text;
        }

        internal void ApplyCoopBoard(FactoryCoopCraneState state)
        {
            boardStatus.text = state.Status;
            boardCount.text = state.Counter;
        }

        void UpdateLoadGauge(float dt)
        {
            if (!operating || game.PlayerBlocked || game.CameraOverride || game.CaptureActive)
            {
                if (loadGauge) loadGauge.gameObject.SetActive(false);
                return;
            }
            if (!loadGauge)
            {
                var hud = game.mainMenu ? game.mainMenu.shiftHud : null;
                if (!hud) return;
                loadGauge = FactoryCraneGauge.Create(hud.transform);
            }
            loadGauge.gameObject.SetActive(true);
            var deflection = load.Attached || load.Spilled ? loadDeflection : Vector2.zero;
            // At the angle limit the gauge is orange. Time spent over it fills the final band.
            float stress = load.Attached ? .8f * Mathf.Clamp01(swingRatio) + .2f * Mathf.Clamp01(spillTime / OverloadHold) : 0f;
            loadGauge.Present(deflection, stress, load.Spilled, dt);
        }

        void SampleLoadSwing()
        {
            Vector3 span = hook.position - tip.position;
            swingRatio = Vector3.Angle(Vector3.down, span) / spillAngle;
            var cabOffset = new Vector2(Vector3.Dot(span, slew.right), Vector3.Dot(span, slew.forward));
            loadDeflection = cabOffset.normalized * Mathf.Clamp01(swingRatio);
        }

        void OnDisable()
        {
            if (loadGauge) loadGauge.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (loadGauge) Destroy(loadGauge.gameObject);
        }

        void UpdateLoad(float dt)
        {
            if (resetLoad)
            {
                RestoreLoad();
                hoistLine.ResetAt(tip.position, Mathf.Min(hoistLine.Length, 3f));
                return;
            }

            if (load.Body.position.y < loadHome.position.y - 3f)
            {
                RestoreLoad();
                return;
            }

            if (load.Spilled)
            {
                recoverTime -= dt;
                if (recoverTime <= 0f) RestoreLoad();
                return;
            }

            Vector3 offset = hook.position - load.Eye;
            if (offset.sqrMagnitude > .53f * .53f) mustClearHook = false;
            if (load.Attached)
            {
                SampleLoadSwing();
                if (releaseLoad)
                {
                    Vector3 movement = load.Body.linearVelocity;
                    movement.y = 0f;
                    bool motorRunning = Mathf.Abs(railVelocity) > .85f || Mathf.Abs(slewVelocity) > 9f;
                    if (motorRunning || (!OnTrailer() && movement.sqrMagnitude > 1.12f))
                    {
                        SpillLoad("Released the pallet while the crane was moving.");
                        return;
                    }
                    load.Detach();
                    mustClearHook = true;
                    attemptedLanding = true;
                    settleTime = 0f;
                    return;
                }
                latchTime += dt;
                spillTime = latchTime > .42f && swingRatio >= 1f ? spillTime + dt : 0f;
                if (spillTime >= OverloadHold) SpillLoad("Swung the crane load too hard.");
                return;
            }

            if (attemptedLanding)
            {
                bool still = load.Body.linearVelocity.sqrMagnitude <= .15f && load.Body.angularVelocity.sqrMagnitude <= .3f;
                settleTime = still ? settleTime + dt : 0f;
                if (settleTime >= .5f)
                {
                    attemptedLanding = false;
                    if (OnTrailer())
                    {
                        load.Secure();
                        carriedOnTruck = load.Body.position - truck.position;
                        deliveries++;
                        dispatchTime = .9f;
                        truckPhase = TruckPhase.Dispatching;
                        return;
                    }
                    if (!landingFault) Fault("Set the pallet down outside the marked trailer bay.");
                }
            }

            // The hook and the pallet eye meet at the center coupling.
            if (operating && !load.OnForklift && !mustClearHook && !attemptedLanding && offset.sqrMagnitude <= .29f * .29f &&
                Vector3.Dot(load.Body.rotation * Vector3.up, Vector3.up) > .7f)
            {
                load.Attach(hook);
                SampleLoadSwing();
                latchTime = spillTime = 0f;
                landingFault = false;
            }
        }

        void SpillLoad(string reason)
        {
            load.Spill();
            recoverTime = 7f;
            Fault(reason);
        }

        bool OnTrailer()
        {
            Bounds zone = trailerZone.bounds;
            Bounds footprint = load.Footprint;
            return footprint.min.x >= zone.min.x && footprint.max.x <= zone.max.x &&
                footprint.min.z >= zone.min.z && footprint.max.z <= zone.max.z &&
                Mathf.Abs(footprint.min.y - zone.min.y) <= .12f &&
                Vector3.Dot(load.Body.rotation * Vector3.up, Vector3.up) > .94f;
        }

        internal void CheckForkliftDelivery(FactoryCraneLoad pallet)
        {
            if (pallet != load || truckPhase != TruckPhase.Parked || !OnTrailer()) return;
            attemptedLanding = true;
            landingFault = true;
            settleTime = 0f;
        }

        void UpdateTruck(float dt)
        {
            if (truckPhase == TruckPhase.Dispatching)
            {
                dispatchTime -= dt;
                if (dispatchTime <= 0f) truckPhase = TruckPhase.Departing;
                return;
            }

            bool leaving = truckPhase == TruckPhase.Departing;
            Vector3 target = leaving ? tunnel.position : dock.position;
            Vector3 next = Vector3.MoveTowards(truck.position, target, (leaving ? 8.5f : 7f) * dt);
            truck.MovePosition(next);
            if (leaving) load.Body.MovePosition(next + carriedOnTruck);
            if (next != target) return;

            if (leaving)
            {
                load.gameObject.SetActive(false);
                truckDamage.Restore(TruckPaint[deliveries % TruckPaint.Length]);
                truckPhase = TruckPhase.Reversing;
            }
            else
            {
                RestoreLoad();
                truckPhase = TruckPhase.Parked;
            }
        }

        void RestoreLoad()
        {
            load.ResetLoad(this, loadHome);
            mustClearHook = attemptedLanding = landingFault = false;
            settleTime = latchTime = spillTime = 0f;
            loadDeflection = Vector2.zero;
            swingRatio = 0f;
        }

        public void Hit(FactoryCraneImpact target, float speed, bool fromAbove)
        {
            if (truckPhase != TruckPhase.Parked || speed < (fromAbove ? 3.6f : 2.3f)) return;
            target.Damage(speed);
            if (attemptedLanding) landingFault = true;
            Fault(target.FaultMessage);
        }

        void Fault(string reason)
        {
            if (faultDelay > 0f) return;
            if (game.RegisterMistake(reason, .5f)) faultDelay = 1.05f;
        }
    }
}
