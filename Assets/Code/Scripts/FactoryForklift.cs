using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class FactoryForklift : MonoBehaviour
    {
        [SerializeField] FactoryCraneJob job;
        [SerializeField] CardboardFactoryGame game;
        [SerializeField] Rigidbody body;
        [SerializeField] BoxCollider floor;
        [SerializeField] BoxCollider[] tines;
        [SerializeField] Transform carriage, seat, exit, parking;
        [SerializeField] Transform[] frontSteering, wheelDrums;

        const float LowestFork = .088f;
        const float HighestFork = 2.55f;
        const float WheelRadius = .34f;
        const float ContactRadius = .16f;
        const float VehicleMass = 220f;
        const float TireGrip = 5f;
        const float SideClearance = .68f;
        const float ContactSkin = .006f;

        readonly RaycastHit[] contacts = new RaycastHit[32];
        CardboardRunner driver;
        FactoryCraneLoad carried;
        PhysicsMaterial tireMaterial;
        Vector3 loadOffset;
        Vector2 driveInput;
        float frontAxle, rearAxle;
        float hoistInput, steering, wheelAngle;

        internal bool Driving => driver;

        void Awake()
        {
            body.mass = VehicleMass;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = .12f;
            body.angularDamping = .45f;
            body.centerOfMass = Vector3.up * .44f;
            body.solverIterations = 16;
            body.solverVelocityIterations = 8;

            tireMaterial = new PhysicsMaterial("Forklift tire contact")
            {
                // Traction is applied at the axles so steering does not fight static contact friction.
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            foreach (var wheel in wheelDrums)
            {
                Vector3 hub = transform.InverseTransformPoint(wheel.position);
                var contact = gameObject.AddComponent<SphereCollider>();
                contact.center = new Vector3(hub.x, ContactRadius, hub.z);
                contact.radius = ContactRadius;
                contact.sharedMaterial = tireMaterial;
                if (hub.z > 0f) frontAxle = hub.z;
                else rearAxle = hub.z;
            }
            body.ResetInertiaTensor();
            var inertia = body.inertiaTensor;
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = inertia;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void OnDestroy()
        {
            if (tireMaterial) Destroy(tireMaterial);
        }

        internal bool TryGetDriverSeat(Transform player, out Vector3 position, out float yaw)
        {
            position = seat.position;
            yaw = transform.eulerAngles.y;
            return driver && driver.transform == player;
        }

        internal bool Enter(CardboardRunner player)
        {
            if (!player || driver) return false;
            driver = player;
            driver.PlaceAtWorkAnchor(seat.position, false);
            var view = player == CardboardRunner.Player && Camera.main ? Camera.main.GetComponent<CardboardCamera>() : null;
            if (view)
            {
                view.transform.rotation = Quaternion.LookRotation(transform.forward - Vector3.up * .16f);
                view.AdoptCurrentView();
            }
            return true;
        }

        internal void LeaveSeat() => Dismount(exit.position);

        void Dismount(Vector3 point)
        {
            if (!driver) return;
            driver.PlaceAtWorkAnchor(point, true);
            driver = null;
            driveInput = Vector2.zero;
            hoistInput = 0f;
        }

        internal void ResetVehicle()
        {
            LeaveSeat();
            if (carried) carried.SetDownFromForks();
            carried = null;
            body.position = parking.position;
            body.rotation = parking.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            SetForkHeight(LowestFork);
            steering = wheelAngle = 0f;
            PoseWheels();
        }

        void Update()
        {
            if (!Driving || game.MultiplayerPrototype) return;
            var keyboard = Keyboard.current;
            if (game.PlayerBlocked || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked || keyboard == null)
            {
                driveInput = Vector2.zero;
                hoistInput = 0f;
                return;
            }
            ReadControls(keyboard, Mouse.current);
        }

        void ReadControls(Keyboard keyboard, Mouse mouse)
        {
            Control(keyboard.tabKey.wasPressedThisFrame,
                Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed), Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed),
                mouse == null ? 0f : Axis(mouse.leftButton.isPressed, mouse.rightButton.isPressed));
        }

        internal void Control(bool dismount, float steer, float forward, float lift)
        {
            if (dismount)
            {
                driveInput = Vector2.zero;
                hoistInput = 0f;
                Vector3 left = exit.position;
                Vector3 otherSide = exit.localPosition;
                otherSide.x = -otherSide.x;
                Vector3 right = transform.TransformPoint(otherSide);
                if (CanDismount(left)) Dismount(left);
                else if (CanDismount(right)) Dismount(right);
                return;
            }
            driveInput = new Vector2(steer, forward);
            hoistInput = lift;
        }

        internal void AddCoopParts(System.Collections.Generic.List<Transform> parts)
        {
            parts.Add(body.transform);
            parts.Add(carriage);
            parts.AddRange(frontSteering);
            parts.AddRange(wheelDrums);
        }

        static bool CanDismount(Vector3 point) => !Physics.CheckCapsule(point + Vector3.up * .34f,
            point + Vector3.up * 1.4f, .29f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        void FixedUpdate() => Advance(Time.fixedDeltaTime);

        void Advance(float dt)
        {
            if (carried && !carried.OnForklift) carried = null;
            steering = Mathf.MoveTowards(steering, driveInput.x * 34f, 145f * dt);
            Vector3 forward = body.rotation * Vector3.forward;
            float speed = Vector3.Dot(body.linearVelocity, forward);
            if (Driving || !body.IsSleeping()) ApplyTraction(forward, speed);
            wheelAngle = Mathf.Repeat(wheelAngle + speed * dt / WheelRadius * Mathf.Rad2Deg, 360f);
            PoseWheels();
            if (hoistInput != 0f) Lift(dt);
            if (driver) driver.transform.position = body.position + body.rotation * seat.localPosition;
        }

        void ApplyTraction(Vector3 forward, float speed)
        {
            float targetSpeed = driveInput.y * (driveInput.y < 0f ? 2.4f : 3.6f);
            var bounds = floor.bounds;
            Vector3 position = body.position;
            Vector3 direction = forward * Mathf.Sign(targetSpeed);
            // Brake at the open ends of the hall without teleporting the chassis.
            Vector3 projected = position + direction * .65f;
            if ((direction.x > 0f && projected.x > bounds.max.x - SideClearance) ||
                (direction.x < 0f && projected.x < bounds.min.x + SideClearance) ||
                (direction.z > 0f && projected.z > bounds.max.z - SideClearance) ||
                (direction.z < 0f && projected.z < bounds.min.z + SideClearance))
                targetSpeed = 0f;

            float limit = Mathf.Abs(targetSpeed) > Mathf.Abs(speed) && targetSpeed * speed >= 0f ? 4.4f : 8f;
            float acceleration = Mathf.Clamp((targetSpeed - speed) * 13f, -limit, limit);
            if (Mathf.Abs(acceleration) > .015f) body.AddForce(forward * (VehicleMass * acceleration));

            Vector3 right = body.rotation * Vector3.right;
            Vector3 frontRight = Quaternion.AngleAxis(steering, Vector3.up) * right;
            ApplyAxleGrip(position + forward * frontAxle, frontRight);
            ApplyAxleGrip(position + forward * rearAxle, right);
        }

        void ApplyAxleGrip(Vector3 position, Vector3 right)
        {
            float slip = Vector3.Dot(body.GetPointVelocity(position), right);
            if (Mathf.Abs(slip) < .002f) return;
            float correction = Mathf.Clamp(slip * TireGrip, -6f, 6f);
            body.AddForceAtPosition(-right * (VehicleMass * .5f * correction), position);
        }

        void Lift(float dt)
        {
            float height = carriage.localPosition.y;
            float target = Mathf.Clamp(height + hoistInput * 1.25f * dt, LowestFork, HighestFork);
            float step = target - height;
            if (Mathf.Abs(step) < .00001f) return;
            if (step > 0f && !carried) TryPickUp();

            float fraction = 1f;
            foreach (var tine in tines) fraction = Mathf.Min(fraction, SweepVertical(tine, step));
            bool landed = false;
            if (carried)
            {
                float loadTravel = SweepVertical(carried.ForkliftBounds, step);
                landed = step < 0f && loadTravel < 1f && loadTravel <= fraction;
                fraction = Mathf.Min(fraction, loadTravel);
            }
            SetForkHeight(height + step * fraction);
            if (!carried) return;

            if (landed || (step < 0f && carriage.localPosition.y <= LowestFork + .0001f))
            {
                var pallet = carried;
                carried = null;
                pallet.SetDownFromForks();
                job.CheckForkliftDelivery(pallet);
                return;
            }
            carried.MoveForkAnchor(carriage.localPosition + loadOffset);
        }

        void TryPickUp()
        {
            var pallet = job.PickupPallet;
            if (!pallet || !pallet.ForkliftBounds || pallet.OnForklift) return;
            var deck = pallet.Body;
            var forward = body.rotation * Vector3.forward;
            var right = body.rotation * Vector3.right;
            var carriagePosition = body.position + body.rotation * carriage.localPosition;
            var relative = deck.position - carriagePosition;
            float aligned = Mathf.Abs(Vector3.Dot(forward, deck.rotation * Vector3.forward));
            float crosswise = Mathf.Abs(Vector3.Dot(forward, deck.rotation * Vector3.right));
            if (Mathf.Max(aligned, crosswise) < .991f || Mathf.Abs(Vector3.Dot(relative, right)) > .095f) return;
            float halfDepth = (aligned >= crosswise ? pallet.ForkliftBounds.size.z : pallet.ForkliftBounds.size.x) * .5f;
            float frontGap = Vector3.Dot(relative, forward) - halfDepth;
            float slotHeight = -relative.y;
            if (frontGap < -.07f || frontGap > .47f || slotHeight < .059f || slotHeight > .121f) return;
            Vector3 offset = Quaternion.Inverse(body.rotation) * relative;
            if (!pallet.TryLiftOnForks(body, carriage.localPosition + offset)) return;
            carried = pallet;
            loadOffset = offset;
        }

        float SweepVertical(BoxCollider shape, float step)
        {
            var ownBody = shape.attachedRigidbody;
            var center = shape.transform.TransformPoint(shape.center);
            var fromRenderPose = ownBody.rotation * Quaternion.Inverse(ownBody.transform.rotation);
            center = ownBody.position + fromRenderPose * (center - ownBody.transform.position);
            float distance = Mathf.Abs(step);
            Vector3 direction = step > 0f ? Vector3.up : Vector3.down;
            var half = Vector3.Scale(shape.size * .5f, shape.transform.lossyScale) - Vector3.one * .003f;
            int count = Physics.BoxCastNonAlloc(center, half, direction, contacts,
                fromRenderPose * shape.transform.rotation, distance + ContactSkin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float limit = distance;
            for (int i = 0; i < count; i++)
            {
                var hit = contacts[i];
                var otherBody = hit.collider.attachedRigidbody;
                if (otherBody == body || (carried && otherBody == carried.Body)) continue;
                if (hit.distance <= .0001f && Vector3.Dot(direction, hit.normal) >= 0f) continue;
                limit = Mathf.Min(limit, Mathf.Max(0f, hit.distance - ContactSkin));
            }
            return limit / distance;
        }

        void SetForkHeight(float height)
        {
            var offset = carriage.localPosition;
            offset.y = height;
            carriage.localPosition = offset;
        }

        void PoseWheels()
        {
            foreach (var pivot in frontSteering) pivot.localRotation = Quaternion.Euler(0f, steering, 0f);
            foreach (var wheel in wheelDrums) wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        }
    }
}
