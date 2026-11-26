using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    internal enum ProductionPieceStage { Raw, Flattened, Toothed, Finished }

    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(MeshFilter))]
    public sealed class FactoryProductionPiece : MonoBehaviour
    {
        const int ToothCount = 12;
        const float RawThickness = .19f;
        const float SheetThickness = .105f;
        const float ToothRadius = .44f;
        const float RootRadius = .342f;
        const float BoreRadius = .117f;

        static readonly Vector2[] RawOutline =
        {
            new Vector2(-.32f, -.31f), new Vector2(.26f, -.35f), new Vector2(.38f, -.18f),
            new Vector2(.35f, .27f), new Vector2(.16f, .35f), new Vector2(-.29f, .32f),
            new Vector2(-.38f, .13f), new Vector2(-.38f, -.12f)
        };

        static readonly Vector2[] SheetOutline =
        {
            new Vector2(-.42f, -.47f), new Vector2(.37f, -.47f), new Vector2(.48f, -.37f),
            new Vector2(.48f, .39f), new Vector2(.41f, .47f), new Vector2(-.41f, .47f),
            new Vector2(-.48f, .41f), new Vector2(-.48f, -.38f)
        };

        static readonly float[] ToothAngles = { .06f, .25f, .61f, .80f };
        static readonly Vector2[] GearOutline = BuildGearOutline();

        BoxCollider envelope;
        GameObject boreColliders;
        MeshFilter shape, stencil;
        Mesh shapeMesh, stencilMesh;

        internal Rigidbody Body { get; private set; }
        internal ProductionPieceStage Stage { get; private set; }
        internal bool IsLoose => !Body.isKinematic;
        internal float HalfThickness => (Stage == ProductionPieceStage.Raw ? RawThickness : SheetThickness) * .5f;

        internal static FactoryProductionPiece Create(Transform workshopParent, Vector3 worldPosition,
            Material cardboard, Material cutEdge, Material ink)
        {
            var item = new GameObject("Thick cardboard blank");
            item.SetActive(false);
            item.transform.SetParent(workshopParent, false);
            item.transform.position = worldPosition;
            var piece = item.AddComponent<FactoryProductionPiece>();
            piece.Body = item.GetComponent<Rigidbody>();
            piece.Body.linearDamping = .16f;
            piece.Body.angularDamping = .6f;
            piece.Body.interpolation = RigidbodyInterpolation.Interpolate;
            piece.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            piece.envelope = item.GetComponent<BoxCollider>();
            piece.shape = item.GetComponent<MeshFilter>();
            item.AddComponent<MeshRenderer>().sharedMaterials = new[] { cardboard, cutEdge };

            var mark = new GameObject("Ink stencil");
            mark.transform.SetParent(item.transform, false);
            piece.stencil = mark.AddComponent<MeshFilter>();
            var renderer = mark.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            piece.SetStage(ProductionPieceStage.Raw);
            item.SetActive(true);
            return piece;
        }

        internal void Hold(Transform anchor, Vector3 localPosition, Quaternion localRotation)
        {
            if (IsLoose)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.detectCollisions = false;
            Body.interpolation = RigidbodyInterpolation.None;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
            transform.SetParent(anchor, false);
            transform.SetLocalPositionAndRotation(localPosition, localRotation);
            Body.position = transform.position;
            Body.rotation = transform.rotation;
        }

        internal void Release(Transform workshopParent, Vector3 velocity = default)
        {
            transform.SetParent(workshopParent, true);
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            Body.isKinematic = false;
            Body.detectCollisions = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity = velocity;
            Body.WakeUp();
        }

        internal void SetStage(ProductionPieceStage stage)
        {
            if (shapeMesh && Stage == stage) return;
            Stage = stage;
            ClearMeshes();
            float thickness = HalfThickness * 2f;
            switch (stage)
            {
                case ProductionPieceStage.Raw:
                    name = "Thick cardboard blank";
                    shapeMesh = FactoryKitchenSlice.CreateMesh(RawOutline, thickness);
                    Body.mass = .92f;
                    break;
                case ProductionPieceStage.Flattened:
                    name = "Flattened cardboard with tooth stencil";
                    shapeMesh = FactoryKitchenSlice.CreateMesh(SheetOutline, thickness);
                    stencilMesh = BuildStencil(HalfThickness);
                    Body.mass = .92f;
                    break;
                case ProductionPieceStage.Toothed:
                    name = "Unbored cardboard gear";
                    shapeMesh = BuildGear(thickness, false);
                    Body.mass = .64f;
                    break;
                default:
                    name = "Finished cardboard gear";
                    shapeMesh = BuildGear(thickness, true);
                    Body.mass = .59f;
                    break;
            }
            shapeMesh.name = name;
            shape.sharedMesh = shapeMesh;
            stencil.sharedMesh = stencilMesh;
            envelope.center = shapeMesh.bounds.center;
            envelope.size = shapeMesh.bounds.size;
            envelope.enabled = stage != ProductionPieceStage.Finished;
            if (!envelope.enabled && !boreColliders) CreateBoreColliders(thickness);
            if (boreColliders) boreColliders.SetActive(!envelope.enabled);
        }

        void CreateBoreColliders(float thickness)
        {
            // Compound colliders keep the bore open on a dynamic rigidbody.
            boreColliders = new GameObject("Gear ring collision");
            boreColliders.transform.SetParent(transform, false);
            const float outside = .40f;
            for (int i = 0; i < ToothCount; i++)
            {
                float angle = i * (360f / ToothCount);
                var sector = new GameObject("Gear sector").transform;
                sector.SetParent(boreColliders.transform, false);
                sector.localRotation = Quaternion.Euler(0f, angle, 0f);
                sector.localPosition = sector.localRotation * Vector3.forward * ((BoreRadius + outside) * .5f);
                sector.gameObject.AddComponent<BoxCollider>().size = new Vector3(.174f, thickness, outside - BoreRadius);
            }
        }

        static Vector2[] BuildGearOutline()
        {
            var points = new Vector2[ToothCount * ToothAngles.Length];
            for (int i = 0; i < ToothCount; i++)
            for (int corner = 0; corner < ToothAngles.Length; corner++)
            {
                float angle = (i + ToothAngles[corner]) * (2f * Mathf.PI / ToothCount);
                float distance = corner == 1 || corner == 2 ? ToothRadius : RootRadius;
                points[i * ToothAngles.Length + corner] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }
            return points;
        }

        static Mesh BuildGear(float thickness, bool bored)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var faces = new List<int>();
            var edges = new List<int>();
            float y = thickness * .5f;

            for (int i = 0; i < GearOutline.Length; i++)
            {
                var a = GearOutline[i];
                var b = GearOutline[(i + 1) % GearOutline.Length];
                Vector2 innerA = bored ? a.normalized * BoreRadius : Vector2.zero;
                Vector2 innerB = bored ? b.normalized * BoreRadius : Vector2.zero;
                if (bored)
                {
                    AddFace(vertices, uv, faces, y, a, innerA, innerB, b);
                    AddFace(vertices, uv, faces, -y, b, innerB, innerA, a);
                    AddEdge(vertices, uv, edges, innerB, innerA, y);
                }
                else
                {
                    AddFace(vertices, uv, faces, y, a, Vector2.zero, b);
                    AddFace(vertices, uv, faces, -y, b, Vector2.zero, a);
                }
                AddEdge(vertices, uv, edges, a, b, y);
            }

            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(faces, 0);
            mesh.SetTriangles(edges, 1);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddFace(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, float height,
            Vector2 a, Vector2 b, Vector2 c, Vector2? d = null)
        {
            int first = vertices.Count;
            vertices.Add(OnPlane(a, height)); uv.Add(a * 3f);
            vertices.Add(OnPlane(b, height)); uv.Add(b * 3f);
            vertices.Add(OnPlane(c, height)); uv.Add(c * 3f);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            if (!d.HasValue) return;
            vertices.Add(OnPlane(d.Value, height)); uv.Add(d.Value * 3f);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        static void AddEdge(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector2 a, Vector2 b, float halfThickness)
        {
            int first = vertices.Count;
            vertices.Add(OnPlane(a, halfThickness));
            vertices.Add(OnPlane(b, halfThickness));
            vertices.Add(OnPlane(b, -halfThickness));
            vertices.Add(OnPlane(a, -halfThickness));
            float span = Vector2.Distance(a, b) * 6f;
            uv.Add(Vector2.up);
            uv.Add(new Vector2(span, 1f));
            uv.Add(new Vector2(span, 0f));
            uv.Add(Vector2.zero);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        static Mesh BuildStencil(float height)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float y = height + .0018f;
            for (int i = 0; i < GearOutline.Length; i++)
                AddInkStroke(vertices, uv, triangles, GearOutline[i], GearOutline[(i + 1) % GearOutline.Length], y);
            AddInkStroke(vertices, uv, triangles, Vector2.left * .050f, Vector2.right * .050f, y);
            AddInkStroke(vertices, uv, triangles, Vector2.down * .050f, Vector2.up * .050f, y);
            var mesh = new Mesh { name = "Tooth stencil ink" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddInkStroke(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector2 a, Vector2 b, float height)
        {
            var direction = (b - a).normalized * .004f;
            var normal = new Vector2(-direction.y, direction.x);
            a -= direction;
            b += direction;
            AddFace(vertices, uv, triangles, height, a + normal, b + normal, b - normal, a - normal);
        }

        static Vector3 OnPlane(Vector2 point, float height) => new Vector3(point.x, height, point.y);

        void ClearMeshes()
        {
            if (shapeMesh) Destroy(shapeMesh);
            if (stencilMesh) Destroy(stencilMesh);
            shapeMesh = stencilMesh = null;
        }

        void OnDestroy() => ClearMeshes();
    }
}
