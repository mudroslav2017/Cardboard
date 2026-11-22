using UnityEngine;

namespace Cardboard
{

    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CranePallet : MonoBehaviour
    {
        [SerializeField] Transform liftEye;

        readonly RaycastHit[] hits = new RaycastHit[24];
        Rigidbody body;
        BoxCollider hull;

        public Vector3 Eye => body.position + body.rotation * liftEye.localPosition;
        public Bounds Footprint => hull.bounds;
        public bool IsStill => body.linearVelocity.sqrMagnitude < .12f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            hull = GetComponent<BoxCollider>();
        }

        public bool CanMove(Vector3 displacement, float speed)
        {
            float distance = displacement.magnitude;
            if (distance < .0001f) return true;

            Vector3 direction = displacement / distance;
            const float skin = .014f;
            int count = Physics.BoxCastNonAlloc(body.position + hull.center, hull.size * .5f - Vector3.one * skin,
                direction, hits, body.rotation, distance + skin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            RaycastHit nearest = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == hull || hit.distance >= closest || Vector3.Dot(direction, hit.normal) >= -.01f)
                    continue;
                closest = hit.distance;
                nearest = hit;
            }

            if (!nearest.collider) return true;
            var surface = nearest.collider.GetComponentInParent<CraneDamageSurface>();
            if (surface)
                surface.Impact(speed * Vector3.Dot(-direction, nearest.normal), nearest.normal.y > .65f);
            return false;
        }

        public void PlaceUnder(Vector3 hook)
        {
            body.position += hook - Eye;
        }

        public void Attach(Vector3 hook)
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            PlaceUnder(hook);
        }

        public void Release()
        {
            body.isKinematic = false;
            body.WakeUp();
        }

        public void Secure()
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        public void ResetAt(Transform spawn)
        {
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.position = spawn.position;
            body.rotation = spawn.rotation;
            body.WakeUp();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (body.isKinematic) return;
            var surface = collision.collider.GetComponentInParent<CraneDamageSurface>();
            if (surface && collision.contactCount > 0)
            {
                Vector3 normal = collision.GetContact(0).normal;
                surface.Impact(Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)), normal.y > .65f);
            }
        }
    }
}
