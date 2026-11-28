using System;
using UnityEngine;

namespace Cardboard
{
    public sealed class FactoryCraneCable : MonoBehaviour
    {
        [Serializable]
        struct Link
        {
            public Rigidbody body;
            public ConfigurableJoint joint;
            public Transform sleeve, collar;
            [NonSerialized] public MeshFilter mesh;
        }

        [SerializeField] Rigidbody anchor, hook;
        [SerializeField] SphereCollider hookContact;
        [SerializeField] ConfigurableJoint hookJoint;
        [SerializeField] Link[] links;
        [SerializeField] float initialLength = 6.86f;

        const float MinimumLength = 1.52f;
        const float MaximumLength = 8.22f;
        float length, obstructionTime;
        int firstLink;
        bool clearingObstruction;
        Mesh fullSleeve, drumSleeve;
        Vector3[] fullVertices, drumVertices;

        public float InitialLength => initialLength;
        public float Length => length > 0f ? length : initialLength;
        internal void AddCoopParts(System.Collections.Generic.List<Transform> parts)
        {
            foreach (var link in links) parts.Add(link.body.transform);
        }

        void Awake()
        {
            float half = LinkLength * .5f;
            for (int i = 0; i < links.Length; i++)
            {
                var sleeve = links[i].sleeve;
                links[i].mesh = sleeve.GetComponent<MeshFilter>();
                var scale = sleeve.localScale;
                scale.y = half;
                sleeve.localScale = scale;
                links[i].collar.localPosition = Vector3.down * (half - .022f);
            }
            fullSleeve = links[0].mesh.sharedMesh;
            drumSleeve = Instantiate(fullSleeve);
            drumSleeve.MarkDynamic();
            fullVertices = fullSleeve.vertices;
            drumVertices = (Vector3[])fullVertices.Clone();
            ResetAt(anchor.position, initialLength);
        }

        void OnDestroy()
        {
            if (drumSleeve) Destroy(drumSleeve);
        }

        public void ResetAt(Vector3 tip, float requestedLength)
        {
            length = Mathf.Clamp(requestedLength, MinimumLength, MaximumLength);
            firstLink = FirstLinkAt(length);
            anchor.position = tip;
            float paidOut = ExposedLength(length, firstLink);
            float half = LinkLength * .5f;
            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                link.body.gameObject.SetActive(i >= firstLink);
                if (i < firstLink) continue;
                if (i == firstLink) ConnectToDrum(link, paidOut);
                else ConnectToLink(link, links[i - 1].body);
                ResetBody(link.body, tip - Vector3.up * (paidOut + (i - firstLink) * LinkLength - half));
            }
            hookJoint.connectedAnchor = Vector3.down * half;
            ResetBody(hook, tip - Vector3.up * Length);
            hookContact.enabled = false;
            hookContact.enabled = !Physics.CheckSphere(hook.position, hookContact.radius + .015f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            clearingObstruction = !hookContact.enabled;
            obstructionTime = 0f;
        }

        static void ResetBody(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        public void Drive(Vector3 tip, float windingRate, bool loaded, float dt)
        {
            anchor.MovePosition(tip);
            SetLength(Length - windingRate * dt);

            if (loaded)
            {
                hookContact.enabled = false;
                clearingObstruction = true;
                obstructionTime = 0f;
                return;
            }

            var tilt = Vector3.Cross(hook.rotation * Vector3.up, Vector3.up);
            var tumbling = Vector3.ProjectOnPlane(hook.angularVelocity, Vector3.up);
            hook.AddTorque(tilt * 17f - tumbling * 4f, ForceMode.Acceleration);

            // A catch on a truck edge must not keep an empty hook pinned below the hoist.
            bool stalled = windingRate > .01f && Vector3.Distance(tip, hook.position) > Length + .13f &&
                hook.linearVelocity.y < .24f;
            obstructionTime = stalled ? obstructionTime + dt : 0f;
            if (obstructionTime >= .20f) clearingObstruction = true;
            if (!hookContact.enabled && !Physics.CheckSphere(hook.position, hookContact.radius + .025f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                clearingObstruction = false;
                obstructionTime = 0f;
            }
            hookContact.enabled = !clearingObstruction;
        }

        float LinkLength => (MaximumLength - hookJoint.anchor.y) / links.Length;

        int FirstLinkAt(float lineLength)
        {
            float deployed = (lineLength - hookJoint.anchor.y) / LinkLength;
            return links.Length - Mathf.Clamp(Mathf.CeilToInt(deployed), 1, links.Length);
        }

        float ExposedLength(float lineLength, int head) =>
            Mathf.Clamp(lineLength - hookJoint.anchor.y - (links.Length - head - 1) * LinkLength, 0f, LinkLength);

        void SetLength(float requestedLength)
        {
            float target = Mathf.Clamp(requestedLength, MinimumLength, MaximumLength);
            if (Mathf.Abs(length - target) < .0001f) return;
            int nextHead = FirstLinkAt(target);
            float exposed = ExposedLength(target, nextHead);

            for (int i = firstLink - 1; i >= nextHead; i--)
                PayOutLink(links[i], links[i + 1]);

            ConnectToDrum(links[nextHead], exposed);
            for (int i = firstLink; i < nextHead; i++)
                links[i].body.gameObject.SetActive(false);

            firstLink = nextHead;
            length = target;
        }

        void PayOutLink(Link link, Link below)
        {
            // Match the moving joint on the hanging chain before handing it off from the drum.
            var rotation = below.body.rotation;
            var half = rotation * Vector3.up * (LinkLength * .5f);
            var pin = below.body.position + half;
            var angular = below.body.angularVelocity;
            link.body.gameObject.SetActive(true);
            link.body.position = pin + half;
            link.body.rotation = rotation;
            link.body.angularVelocity = angular;
            link.body.linearVelocity = below.body.GetPointVelocity(pin) + Vector3.Cross(angular, half);
            link.body.WakeUp();
            ConnectToLink(below, link.body);
        }

        void ConnectToDrum(Link link, float exposed)
        {
            if (link.joint.connectedBody != anchor)
            {
                link.joint.connectedBody = anchor;
                link.joint.connectedAnchor = Vector3.zero;
            }
            // Slide the point held by the drum along this rigid link as it feeds through.
            link.joint.anchor = Vector3.up * (exposed - LinkLength * .5f);

            // Trim only the part hidden inside the drum, without scaling the hanging links.
            float cut = Mathf.Lerp(fullSleeve.bounds.min.y, fullSleeve.bounds.max.y, exposed / LinkLength);
            for (int i = 0; i < drumVertices.Length; i++) drumVertices[i].y = Mathf.Min(fullVertices[i].y, cut);
            drumSleeve.SetVertices(drumVertices);
            if (link.mesh.sharedMesh != drumSleeve) link.mesh.sharedMesh = drumSleeve;
            link.collar.gameObject.SetActive(exposed > .05f);
        }

        void ConnectToLink(Link link, Rigidbody previous)
        {
            float half = LinkLength * .5f;
            link.joint.connectedBody = previous;
            link.joint.connectedAnchor = Vector3.down * half;
            link.joint.anchor = Vector3.up * half;
            link.mesh.sharedMesh = fullSleeve;
            link.collar.gameObject.SetActive(true);
        }
    }
}
