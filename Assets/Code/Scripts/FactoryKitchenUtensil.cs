using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FactoryKitchenUtensil : MonoBehaviour
    {
        public enum Kind { Knife, Spatula, Ladle }

        public Kind Tool { get; private set; }
        public FactoryPhysicsProp Prop { get; private set; }
        public bool Dipping { get; private set; }
        public float Tilt { get; private set; }
        public Vector3 Tip => Prop.Body.position + Prop.Body.rotation * tip;
        public Vector3 Opening => Prop.Body.rotation * Vector3.up;
        FactoryKitchen kitchen;
        FactoryOperatorHand rightHand;
        Transform rightGrip;
        Collider blade;
        Mesh spatulaWedge;
        FactoryKitchenFood spatulaContact;
        ConfigurableJoint spatulaSupport;
        Quaternion spatulaPickupRotation;
        float spatulaPickupHeight;
        bool wasDipping;
        Pose home;
        Vector3 tip, previousVelocity, previousAngularVelocity;
        float angle, lastStrike, strikeTravel, lastTap;

        internal static FactoryKitchenUtensil Create(FactoryKitchen kitchen, Kind kind, Transform model,
            Vector3 center, Vector3 size, Vector3 tip, Material paper, Material sleeve)
        {
            var body = model.gameObject.AddComponent<Rigidbody>();
            body.mass = kind == Kind.Knife ? .65f : kind == Kind.Spatula ? .58f : .22f;
            var utensil = model.gameObject.AddComponent<FactoryKitchenUtensil>();
            if (kind == Kind.Spatula)
            {
                var shape = model.gameObject.AddComponent<MeshCollider>();
                utensil.spatulaWedge = MakeSpatulaWedge(center, size);
                shape.sharedMesh = utensil.spatulaWedge;
                shape.convex = true;
                utensil.blade = shape;
            }
            else
            {
                var shape = model.gameObject.AddComponent<BoxCollider>();
                shape.center = center;
                shape.size = size;
                utensil.blade = shape;
            }
            utensil.blade.contactOffset = .0015f;
            utensil.kitchen = kitchen;
            utensil.Tool = kind;
            utensil.Prop = model.gameObject.AddComponent<FactoryPhysicsProp>();
            utensil.Prop.itemName = kind.ToString().ToLowerInvariant();
            body.maxAngularVelocity = 24f;
            utensil.tip = tip;
            utensil.home = new Pose(model.position, model.rotation);
            var control = model.gameObject.AddComponent<FactoryKitchenControl>();
            control.kitchen = kitchen;
            control.action = kind == Kind.Knife ? KitchenAction.Knife : kind == Kind.Spatula ? KitchenAction.Spatula : KitchenAction.Serve;
            model.gameObject.AddComponent<FactoryInteractable>().action = FactoryAction.Kitchen;
            if (kind != Kind.Knife)
            {
                utensil.rightGrip = CardboardArt.Group("Right grip", model, new Vector3(.015f, -.012f, -.092f));
                utensil.rightGrip.localRotation = Quaternion.Euler(13, 0, -33);
                utensil.rightHand = FactoryOperatorHand.Create(kitchen.transform, paper, sleeve, false, .62f);
            }
            utensil.Rest();
            return utensil;
        }

        internal bool Take(CardboardFactoryGame game)
        {
            if (!Prop.Take(game)) return false;
            var body = Prop.Body;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GripPose(Camera.main.transform, out var position, out var rotation);
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (rightHand) rightHand.Attach(rightGrip);
            return true;
        }

        internal void Rest()
        {
            ReleaseSpatula(false);
            wasDipping = false;
            if (rightHand) rightHand.Release();
            var body = Prop.Body;
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;
            body.position = home.position;
            body.rotation = home.rotation;
            transform.SetPositionAndRotation(home.position, home.rotation);
            angle = Tilt = strikeTravel = 0;
            Dipping = false;
            lastStrike = lastTap = float.NegativeInfinity;
        }

        internal bool ReadInput(Mouse mouse, Vector2 delta, float sensitivity)
        {
            if (Tool == Kind.Knife)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f) angle = Mathf.Repeat(angle + Mathf.Sign(scroll) * 12f, 360f);
                RecordKnifeMotion(-delta.y * sensitivity);
                return false;
            }
            Dipping = mouse.leftButton.isPressed;
            if (!mouse.rightButton.isPressed) return false;
            Tilt = Mathf.Clamp(Tilt + delta.y * .48f, -125f, 125f);
            return true;
        }

        void RecordKnifeMotion(float motion)
        {
            if (motion < 0 || Time.time - lastStrike > .10f) strikeTravel = 0;
            if (motion <= 0) return;
            strikeTravel += motion;
            lastStrike = Time.time;
        }

        internal void GripPose(Transform view, out Vector3 position, out Quaternion rotation)
        {
            float yaw = view.eulerAngles.y;
            if (Tool == Kind.Knife)
            {
                float pitch = Mathf.DeltaAngle(0, view.eulerAngles.x);
                rotation = Quaternion.Euler(pitch * .36f, yaw + angle, 0);
                position = view.TransformPoint(new Vector3(0, -.13f, .92f));
                return;
            }
            if (Tool == Kind.Ladle)
            {
                if (!kitchen.Service.GetLadlePose(out var end, out rotation))
                {
                    rotation = Quaternion.Euler(0, yaw, 0);
                    end = view.TransformPoint(new Vector3(.22f, -.25f, .79f));
                }
                position = end - rotation * tip;
                return;
            }
            Vector3 forward = view.forward;
            float level = kitchen.SpatulaLevel;
            float distance = forward.y < -.1f ? Mathf.Clamp((view.position.y - level) / -forward.y, .70f, 1.70f) : 1.1f;
            Vector3 target = view.position + forward * distance;
            target.y = Mathf.Max(target.y, level) + (Dipping ? 0 : .235f);
            rotation = Quaternion.Euler(0, yaw, Tilt);
            position = target - rotation * tip;
        }

        internal void SampleVelocity()
        {
            previousVelocity = Prop.Body.linearVelocity;
            previousAngularVelocity = Prop.Body.angularVelocity;
            if (Tool != Kind.Spatula) return;
            if (spatulaSupport && (Dipping || Opening.y < -.43f)) ReleaseSpatula(!Dipping);
            if (Dipping && !wasDipping) spatulaContact = null;
            if (wasDipping && !Dipping && spatulaContact) LiftSpatulaContact();
            wasDipping = Dipping;
        }

        void OnCollisionEnter(Collision collision) => ContactFood(collision);
        void OnCollisionStay(Collision collision) => ContactFood(collision);

        void ContactFood(Collision collision)
        {
            if (!Prop.IsHeld || !kitchen.ToolsActive) return;
            if (Tool == Kind.Knife) Strike(collision);
            if (Tool == Kind.Spatula && Dipping) SlideUnder(collision);
        }

        void Strike(Collision collision)
        {
            if (strikeTravel < 7f || Time.time - lastStrike > .19f) return;

            var food = collision.collider.GetComponentInParent<FactoryKitchenFood>();
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (contact.thisCollider != blade) continue;
                var body = Prop.Body;
                Vector3 local = Quaternion.Inverse(body.rotation) * (contact.point - body.position);
                if (local.y > .039f) continue;
                Vector3 velocity = previousVelocity + Vector3.Cross(previousAngularVelocity, contact.point - body.worldCenterOfMass);
                // On the first impact PhysX may have already stopped the edge against the food.
                float down = Mathf.Max(-velocity.y, Mathf.Abs(collision.relativeVelocity.y));
                if (down < .95f) continue;
                if (food && kitchen.TryCut(food, body.position, body.rotation * Vector3.forward)) return;
                if (!food && Time.time - lastTap > .15f && collision.collider.transform.IsChildOf(kitchen.board.transform))
                {
                    lastTap = Time.time;
                    kitchen.PlayKnifeTap(contact.point);
                    return;
                }
            }
        }

        void SlideUnder(Collision collision)
        {
            if (Opening.y < .86f) return;
            var piece = collision.collider.GetComponentInParent<FactoryKitchenFood>();
            if (!piece || !kitchen.pan.Contains(piece.Prop.Body.position)) return;
            var body = Prop.Body;
            Vector3 forward = body.rotation * Vector3.forward;
            if (Vector3.Dot(previousVelocity, forward) < .13f) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                Vector3 local = Quaternion.Inverse(body.rotation) * (contact.point - body.position);
                if (contact.thisCollider != blade || local.z < tip.z + .03f) continue;
                // Give the thin bevel an upward impulse at its edge so it wedges under resting food.
                var food = piece.Prop.Body;
                Vector3 velocity = food.linearVelocity;
                float forwardPush = Mathf.Max(0, Vector3.Dot(velocity, forward) - .15f);
                float upward = Mathf.Max(0, .58f - velocity.y);
                food.AddForce(Vector3.up * upward - forward * forwardPush, ForceMode.VelocityChange);
                if (!spatulaContact) spatulaContact = piece;
                return;
            }
        }

        void LiftSpatulaContact()
        {
            var piece = spatulaContact;
            spatulaContact = null;
            var body = Prop.Body;
            var food = piece.Prop.Body;
            Vector3 local = Quaternion.Inverse(body.rotation) * (food.position - body.position);
            if (Mathf.Abs(local.x) > .081f || local.z < tip.z - .055f || local.z > tip.z + .105f ||
                local.y < .002f || local.y > .13f) return;

            // The bevel has made contact. The joint models the support of the blade while it lifts,
            // keeping the piece dynamic and letting a hit against another object knock it free.
            spatulaPickupRotation = food.rotation;
            spatulaPickupHeight = food.position.y - kitchen.pan.Prop.Body.position.y;
            spatulaSupport = piece.gameObject.AddComponent<ConfigurableJoint>();
            spatulaSupport.connectedBody = body;
            spatulaSupport.autoConfigureConnectedAnchor = false;
            spatulaSupport.anchor = Vector3.zero;
            spatulaSupport.connectedAnchor = local;
            spatulaSupport.xMotion = spatulaSupport.yMotion = spatulaSupport.zMotion = ConfigurableJointMotion.Locked;
            spatulaSupport.angularXMotion = spatulaSupport.angularYMotion = spatulaSupport.angularZMotion = ConfigurableJointMotion.Locked;
            spatulaSupport.connectedMassScale = .18f;
            spatulaSupport.breakForce = 90f;
            spatulaSupport.breakTorque = 9f;
            spatulaSupport.enableCollision = false;
        }

        void ReleaseSpatula(bool turning)
        {
            spatulaContact = null;
            if (!spatulaSupport) return;
            var piece = spatulaSupport.GetComponent<Rigidbody>();
            spatulaSupport.xMotion = spatulaSupport.yMotion = spatulaSupport.zMotion = ConfigurableJointMotion.Free;
            spatulaSupport.angularXMotion = spatulaSupport.angularYMotion = spatulaSupport.angularZMotion = ConfigurableJointMotion.Free;
            Destroy(spatulaSupport);
            spatulaSupport = null;
            if (!turning || !piece) return;
            Vector3 axis = Prop.Body.rotation * Vector3.forward;
            var opposite = Quaternion.AngleAxis(180f, axis) * spatulaPickupRotation;
            var remaining = opposite * Quaternion.Inverse(piece.rotation);
            remaining.ToAngleAxis(out float degrees, out var rollAxis);
            float height = Mathf.Max(.032f, piece.position.y - kitchen.pan.Prop.Body.position.y - spatulaPickupHeight);
            float rising = piece.linearVelocity.y + .23f;
            float gravity = -Physics.gravity.y;
            float flight = (rising + Mathf.Sqrt(rising * rising + 2f * gravity * height)) / gravity;
            Vector3 roll = rollAxis * (Mathf.DeltaAngle(0, degrees) * Mathf.Deg2Rad / Mathf.Max(.10f, flight));
            piece.AddForce(Vector3.up * .23f, ForceMode.VelocityChange);
            piece.AddTorque(Vector3.ClampMagnitude(roll, 14f) - piece.angularVelocity, ForceMode.VelocityChange);
        }

        static Mesh MakeSpatulaWedge(Vector3 center, Vector3 size)
        {
            float x = size.x * .5f, y = size.y * .5f, z = size.z * .5f;
            var mesh = new Mesh { name = "Spatula beveled edge collision" };
            var vertices = new[]
            {
                new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,-y,z), new Vector3(-x,-y,z),
                new Vector3(-x,y,-z), new Vector3(x,y,-z), new Vector3(x,y,z*.52f), new Vector3(-x,y,z*.52f)
            };
            for (int i = 0; i < vertices.Length; i++) vertices[i] += center;
            mesh.vertices = vertices;
            mesh.triangles = new[]
            {
                0,1,2, 0,2,3, 4,7,6, 4,6,5, 0,4,5, 0,5,1,
                0,3,7, 0,7,4, 1,5,6, 1,6,2, 3,2,6, 3,6,7
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (spatulaWedge) Destroy(spatulaWedge);
        }
    }
}
