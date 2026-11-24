using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(FactoryPhysicsProp))]
    public sealed class FactoryKitchenTool : MonoBehaviour
    {
        public Transform home;
        public bool pan;
        public FactoryPhysicsProp Prop => GetComponent<FactoryPhysicsProp>();
        public bool ReadyAtStation
        {
            get
            {
                var prop = Prop;
                var body = prop.Body;
                if (!body || prop.IsHeld) return false;

                Vector3 offset = home.InverseTransformPoint(body.position);
                bool withinReach = pan ? offset.x * offset.x + offset.z * offset.z <= .54f * .54f :
                    Mathf.Abs(offset.x) <= .66f && Mathf.Abs(offset.z) <= .42f;
                if (!withinReach || offset.y < (pan ? -.21f : -.15f) || offset.y > .15f) return false;
                if (Vector3.Dot(body.rotation * Vector3.up, home.up) < .93f) return false;

                return body.isKinematic || body.linearVelocity.sqrMagnitude <= .10f && body.angularVelocity.sqrMagnitude <= .49f;
            }
        }
        public float Pitch { get; set; }
        float kickAt;
        bool restingForPrep;
        public float CarryPitch => Pitch + Mathf.Sin(Mathf.Clamp01((Time.time - kickAt) / .23f) * Mathf.PI) * -24f;

        public void Kick() { kickAt = Time.time; }

        public void ResetTool()
        {
            Undock();
            Prop.ResetHome();
            Dock();
        }

        public void Dock()
        {
            var body = Prop.Body;
            Freeze(body);
            body.position = home.position;
            body.rotation = home.rotation;
            transform.SetPositionAndRotation(home.position, home.rotation);
            restingForPrep = false;
            Pitch = 0;
            kickAt = float.NegativeInfinity;
        }

        public void Undock()
        {
            var body = Prop.Body;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            restingForPrep = false;
        }

        public void HoldSteadyForPrep()
        {
            var body = Prop.Body;
            if (body.isKinematic) return;
            Freeze(body);
            transform.SetPositionAndRotation(body.position, body.rotation);
            restingForPrep = true;
        }

        public void ResumeAfterPrep()
        {
            if (!restingForPrep) return;
            Undock();
            Prop.Body.useGravity = true;
        }

        static void Freeze(Rigidbody body)
        {
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;
        }

        public bool Contains(Vector3 point)
        {
            var body = Prop.Body;
            Vector3 local = Quaternion.Inverse(body.rotation) * (point - body.position);
            if (local.y < .01f || local.y > .19f) return false;
            if (pan) return new Vector2(local.x, local.z).sqrMagnitude < .30f * .30f;
            return Mathf.Abs(local.x) < .34f && Mathf.Abs(local.z) < .27f;
        }
    }
}
