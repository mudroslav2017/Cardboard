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

        static Mesh shape;
        BoxCollider hitbox;

        internal Rigidbody Body { get; private set; }

        internal static FactoryStapleProjectile Launch(Vector3 position, Quaternion heading, Material material,
            Collider stapler, CardboardRunner owner)
        {
            Vector3 forward = Vector3.ProjectOnPlane(heading * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f)
                forward = Quaternion.Euler(0f, heading.eulerAngles.y, 0f) * Vector3.forward;
            forward.Normalize();

            var shot = new GameObject("Flying staple");
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            if (!shape) shape = BuildShape();
            shot.AddComponent<MeshFilter>().sharedMesh = shape;
            var renderer = shot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            CardboardSurface.Configure(renderer);

            var staple = shot.AddComponent<FactoryStapleProjectile>();
            staple.hitbox = shot.AddComponent<BoxCollider>();
            staple.hitbox.size = new Vector3(Width, Wire, Length);
            var body = shot.AddComponent<Rigidbody>();
            staple.Body = body;
            body.mass = .012f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.useGravity = true;

            if (stapler) Physics.IgnoreCollision(staple.hitbox, stapler);
            if (owner)
            {
                foreach (var collider in owner.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(staple.hitbox, collider);
            }

            body.linearVelocity = forward * Speed + Vector3.up * Rise;
            Destroy(shot, Lifetime);
            return staple;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
            hitbox.enabled = false;
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
