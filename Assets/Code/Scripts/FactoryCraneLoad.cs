using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public sealed class FactoryCraneLoad : MonoBehaviour
    {
        [SerializeField] Transform eye;
        [SerializeField] BoxCollider packedHull, palletFootprint;
        [SerializeField] GameObject packedSlings;
        [SerializeField] Rigidbody[] contents;

        Rigidbody body;
        ConfigurableJoint coupling;
        Vector3[] packedPositions;
        Quaternion[] packedRotations;
        FactoryCraneJob job;

        public Rigidbody Body => body;
        public Vector3 Eye => body.position + body.rotation * eye.localPosition;
        public Bounds Footprint => (palletFootprint ? palletFootprint : packedHull).bounds;
        internal BoxCollider ForkliftBounds => palletFootprint;
        internal bool OnForklift { get; private set; }
        public bool Attached => !OnForklift && coupling && coupling.connectedBody;
        public bool Spilled { get; private set; }
        internal void AddCoopParts(System.Collections.Generic.List<Transform> parts)
        {
            if (packedSlings) parts.Add(packedSlings.transform);
            foreach (var piece in contents) parts.Add(piece.transform);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            coupling = GetComponent<ConfigurableJoint>();
            packedPositions = new Vector3[contents.Length];
            packedRotations = new Quaternion[contents.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                packedPositions[i] = contents[i].transform.localPosition;
                packedRotations[i] = contents[i].transform.localRotation;
            }
        }

        public void ResetLoad(FactoryCraneJob owner, Transform home)
        {
            job = owner;
            gameObject.SetActive(true);
            Detach();
            Spilled = false;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.position = home.position;
            body.rotation = home.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            packedHull.enabled = true;
            if (palletFootprint) palletFootprint.enabled = true;
            if (packedSlings) packedSlings.SetActive(true);
            for (int i = 0; i < contents.Length; i++)
            {
                var piece = contents[i];
                piece.detectCollisions = false;
                piece.interpolation = RigidbodyInterpolation.None;
                piece.isKinematic = true;
                piece.transform.SetParent(transform, false);
                piece.transform.SetLocalPositionAndRotation(packedPositions[i], packedRotations[i]);
            }
        }

        public void Attach(Rigidbody hook)
        {
            // Align at the eye. The payload and hook are both dynamic once the joint is locked.
            body.position += hook.position - Eye;
            body.linearVelocity = hook.linearVelocity;
            body.angularVelocity = hook.angularVelocity;
            coupling.anchor = eye.localPosition;
            coupling.connectedBody = hook;
            coupling.connectedAnchor = Vector3.zero;
            SetCoupling(ConfigurableJointMotion.Locked);
        }

        internal bool TryLiftOnForks(Rigidbody forklift, Vector3 anchor)
        {
            if (!palletFootprint || Attached || Spilled || OnForklift || body.isKinematic) return false;
            coupling.anchor = Vector3.zero;
            coupling.connectedBody = forklift;
            coupling.connectedAnchor = anchor;
            SetCoupling(ConfigurableJointMotion.Locked);
            OnForklift = true;
            body.WakeUp();
            return true;
        }

        internal void MoveForkAnchor(Vector3 anchor) => coupling.connectedAnchor = anchor;

        internal void SetDownFromForks()
        {
            if (OnForklift) Detach();
        }

        public void Detach()
        {
            SetCoupling(ConfigurableJointMotion.Free);
            coupling.connectedBody = null;
            OnForklift = false;
        }

        void SetCoupling(ConfigurableJointMotion motion)
        {
            coupling.xMotion = coupling.yMotion = coupling.zMotion = motion;
            coupling.angularXMotion = coupling.angularYMotion = coupling.angularZMotion = motion;
        }

        public void Spill()
        {
            // Capture velocities before removing the packed collider changes the pallet's center of mass.
            Vector3 linear = body.linearVelocity;
            Vector3 angular = body.angularVelocity;
            Vector3 center = body.worldCenterOfMass;
            Detach();
            Spilled = true;
            packedHull.enabled = false;
            if (palletFootprint) palletFootprint.enabled = false;
            if (packedSlings) packedSlings.SetActive(false);
            body.linearVelocity = linear;
            body.angularVelocity = angular;
            foreach (Rigidbody piece in contents)
            {
                Vector3 position = piece.transform.position;
                Quaternion rotation = piece.transform.rotation;
                piece.transform.SetParent(transform.parent, true);
                piece.isKinematic = false;
                piece.position = position;
                piece.rotation = rotation;
                piece.interpolation = RigidbodyInterpolation.Interpolate;
                piece.detectCollisions = true;
                piece.linearVelocity = linear + Vector3.Cross(angular, position - center);
                piece.angularVelocity = angular;
                piece.WakeUp();
            }
        }

        public void Secure()
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!job || Spilled || collision.contactCount == 0) return;
            var target = collision.collider.GetComponentInParent<FactoryCraneImpact>();
            if (!target) return;
            Vector3 normal = collision.GetContact(0).normal;
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            job.Hit(target, speed, normal.y > .6f);
        }
    }
}
