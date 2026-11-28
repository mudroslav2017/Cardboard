using UnityEngine;

namespace Cardboard
{
    internal sealed class FactoryStapleProjectile : MonoBehaviour
    {
        const float Width = .134f;
        const float Length = .154f;
        const float Wire = .019f;
        const float Speed = 13.6f;
        const float Rise = 2.25f;
        const float Lifetime = 5f;
        const float SoloSpeed = Speed * 1.30f;
        const float SoloScale = 1.50f;
        const float GravityScale = .42f;
        const float PropImpulse = 2.6f;

        static Mesh shape;
        static Material tracerMaterial;
        BoxCollider hitbox;
        TrailRenderer tracer;
        bool solo;
        float expiresAt;
        Vector3 flightVelocity;

        internal Rigidbody Body { get; private set; }

        internal static FactoryStapleProjectile Launch(Vector3 position, Quaternion heading, Material material,
            Collider stapler, CardboardRunner owner)
        {
            var game = CardboardFactoryGame.Instance;
            bool solo = !game || !game.MultiplayerPrototype;
            Vector3 forward = heading * Vector3.forward;
            if (!solo)
            {
                forward = Vector3.ProjectOnPlane(forward, Vector3.up);
                if (forward.sqrMagnitude < .0001f)
                    forward = Quaternion.Euler(0f, heading.eulerAngles.y, 0f) * Vector3.forward;
                forward.Normalize();
                heading = Quaternion.LookRotation(forward);
            }

            var shot = new GameObject("Flying staple");
            shot.transform.SetPositionAndRotation(position, heading);
            if (solo) shot.transform.localScale = Vector3.one * SoloScale;
            if (!shape) shape = BuildShape();
            shot.AddComponent<MeshFilter>().sharedMesh = shape;
            var renderer = shot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            CardboardSurface.Configure(renderer);

            var staple = shot.AddComponent<FactoryStapleProjectile>();
            staple.solo = solo;
            staple.hitbox = shot.AddComponent<BoxCollider>();
            staple.hitbox.size = new Vector3(Width, Wire, Length);
            var body = shot.AddComponent<Rigidbody>();
            staple.Body = body;
            body.mass = .012f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.useGravity = !solo;

            if (stapler) Physics.IgnoreCollision(staple.hitbox, stapler);
            if (owner)
            {
                foreach (var collider in owner.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(staple.hitbox, collider);
            }

            body.linearVelocity = solo ? forward * SoloSpeed : forward * Speed + Vector3.up * Rise;
            staple.flightVelocity = body.linearVelocity;
            if (solo)
            {
                staple.expiresAt = Time.time + Lifetime;
                staple.tracer = CreateTracer(shot.transform, material);
            }
            else Destroy(shot, Lifetime);
            return staple;
        }

        void FixedUpdate()
        {
            if (!solo || Body.isKinematic) return;
            flightVelocity = Body.linearVelocity;
            Body.AddForce(Physics.gravity * GravityScale, ForceMode.Acceleration);
        }

        void Update()
        {
            if (solo && Time.time >= expiresAt) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
            hitbox.enabled = false;
            if (!solo) return;
            tracer.emitting = false;

            Vector3 direction = flightVelocity.sqrMagnitude > .01f ? flightVelocity.normalized : transform.forward;
            var target = collision.collider.GetComponentInParent<FactoryWorkerImpact>();
            if (target)
            {
                float pinnedFor = target.ReceiveStapleImpact(direction);
                if (pinnedFor > 0f)
                {
                    Transform mount = target.StapleMount;
                    Vector3 mark = mount.InverseTransformPoint(Body.position);
                    mark.z = Length * SoloScale * .45f;
                    Body.interpolation = RigidbodyInterpolation.None;
                    transform.SetParent(mount, true);
                    transform.SetLocalPositionAndRotation(mark, Quaternion.Euler(0f, 180f, 0f));
                    expiresAt = Time.time + pinnedFor;
                }
                return;
            }
            if (collision.rigidbody && !collision.rigidbody.isKinematic && collision.contactCount > 0)
                collision.rigidbody.AddForceAtPosition(direction * PropImpulse, collision.GetContact(0).point, ForceMode.Impulse);
        }

        static TrailRenderer CreateTracer(Transform parent, Material fallback)
        {
            if (!tracerMaterial)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) tracerMaterial = new Material(shader) { name = "Staple tracer", hideFlags = HideFlags.HideAndDontSave };
            }
            var root = new GameObject("Staple tracer");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.back * (Length * .45f);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = tracerMaterial ? tracerMaterial : fallback;
            trail.startColor = new Color(1f, .88f, .58f, .92f);
            trail.endColor = new Color(1f, .67f, .35f, 0f);
            trail.startWidth = .045f;
            trail.endWidth = .008f;
            trail.minVertexDistance = .09f;
            trail.time = .14f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        static Mesh BuildShape()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.SetActive(false);
            var cube = template.GetComponent<MeshFilter>().sharedMesh;
            Destroy(template);

            float legOffset = (Width - Wire) * .5f;
            float crownOffset = (Length - Wire) * .5f;
            var legSize = new Vector3(Wire, Wire, Length - Wire);
            var mesh = new Mesh { name = "U-shaped staple" };
            mesh.CombineMeshes(new[]
            {
                Bar(cube, new Vector3(0f, 0f, -crownOffset), new Vector3(Width, Wire, Wire)),
                Bar(cube, new Vector3(-legOffset, 0f, Wire * .5f), legSize),
                Bar(cube, new Vector3(legOffset, 0f, Wire * .5f), legSize)
            });
            return mesh;
        }

        static CombineInstance Bar(Mesh cube, Vector3 center, Vector3 size)
        {
            return new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, Quaternion.identity, size) };
        }
    }
}
