using UnityEngine;

namespace Cardboard
{
    internal sealed class FactoryStaplerModel
    {
        const float OpenLeverAngle = 31f;
        const float ClosedLeverAngle = -5f;

        // Cutting profiles in the side elevation, with +Z toward the nose.
        static readonly Vector2[] ShoeProfile =
        {
            new Vector2(-.292f, -.105f), new Vector2(-.268f, -.123f), new Vector2(.264f, -.123f),
            new Vector2(.302f, -.105f), new Vector2(.302f, -.077f), new Vector2(.242f, -.060f),
            new Vector2(-.267f, -.063f), new Vector2(-.292f, -.079f)
        };
        static readonly Vector2[] NoseProfile =
        {
            new Vector2(.095f, -.091f), new Vector2(.283f, -.095f), new Vector2(.308f, -.060f),
            new Vector2(.308f, .103f), new Vector2(.275f, .140f), new Vector2(.151f, .137f),
            new Vector2(.105f, .098f), new Vector2(.080f, .037f)
        };
        static readonly Vector2[] HandleOutside =
        {
            new Vector2(-.291f, -.064f), new Vector2(-.260f, -.102f), new Vector2(.073f, -.102f),
            new Vector2(.153f, -.074f), new Vector2(.160f, .045f), new Vector2(.096f, .077f),
            new Vector2(-.224f, .069f), new Vector2(-.283f, .031f)
        };
        static readonly Vector2[] HandleInside =
        {
            new Vector2(-.238f, -.044f), new Vector2(-.226f, -.062f), new Vector2(.056f, -.062f),
            new Vector2(.091f, -.043f), new Vector2(.106f, .010f), new Vector2(.071f, .032f),
            new Vector2(-.205f, .023f), new Vector2(-.234f, .012f)
        };
        static readonly Vector2[] LeverProfile =
        {
            new Vector2(-.462f, .010f), new Vector2(-.433f, -.014f),
            new Vector2(.038f, -.020f), new Vector2(.057f, .006f), new Vector2(.035f, .029f),
            new Vector2(-.175f, .036f), new Vector2(-.408f, .043f), new Vector2(-.453f, .033f)
        };

        static Mesh cube, cylinder, shoe, nose, lever;
        static readonly Mesh[] handleRim = new Mesh[HandleOutside.Length];

        internal Transform Lever { get; }
        internal Transform Muzzle { get; }

        internal FactoryStaplerModel(Transform parent, Material body, Material edge, Material grip)
        {
            PrepareMeshes();
            var visual = CardboardArt.Group("Hand stapler", parent, Vector3.zero);
            visual.gameObject.layer = parent.gameObject.layer;
            Profile("Flat magazine shoe", visual, Vector3.zero, shoe, body, edge);
            Profile("Tapered nose housing", visual, Vector3.zero, nose, body, edge);
            for (int i = 0; i < handleRim.Length; i++)
                Profile("Open handle frame", visual, Vector3.zero, handleRim[i], body, edge);

            Box("Magazine lower seam", visual, new Vector3(0f, -.090f, .139f), new Vector3(.205f, .018f, .283f), edge);
            Box("Inset nose face", visual, new Vector3(0f, .018f, .307f), new Vector3(.166f, .165f, .023f), edge);
            Box("Nose plate", visual, new Vector3(0f, .024f, .320f), new Vector3(.136f, .150f, .012f), body);
            var outlet = new Vector3(0f, -.124f, .250f);
            Box("Staple exit slot", visual, outlet, new Vector3(.105f, .007f, .022f), edge);
            Muzzle = CardboardArt.Group("Staple emergence point", visual, outlet + Vector3.down * .006f);
            Muzzle.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Grip pad inside the handle", visual, new Vector3(0f, -.010f, -.235f), new Vector3(.159f, .070f, .031f), grip);
            Box("Lower grip pad", visual, new Vector3(0f, -.063f, -.173f), new Vector3(.159f, .020f, .129f), grip);

            Lever = CardboardArt.Group("Hinged squeeze lever", visual, new Vector3(0f, .128f, .200f));
            Lever.gameObject.layer = visual.gameObject.layer;
            Profile("Raised press handle", Lever, Vector3.zero, lever, body, edge);
            Box("Rust grip strip on the lever", Lever, new Vector3(0f, .039f, -.322f), new Vector3(.118f, .020f, .200f), grip);
            Pin("Lever pivot cover", visual, new Vector3(0f, .128f, .200f), .030f, .218f, body);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .103f;
                Pin("Nose pin", visual, new Vector3(x, -.006f, .255f), .020f, .019f, edge);
                Pin("Casing rivet", visual, new Vector3(x, -.038f, .156f), .015f, .018f, body);
                Pin("Frame rivet", visual, new Vector3(side * .092f, .042f, -.208f), .013f, .016f, body);
            }

            SetSqueeze(0f);
        }

        internal void SetSqueeze(float fraction)
        {
            float angle = Mathf.Lerp(OpenLeverAngle, ClosedLeverAngle, Mathf.Clamp01(fraction));
            Lever.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        static void PrepareMeshes()
        {
            if (shoe) return;
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            shoe = CutProfile("Stapler shoe", .218f, ShoeProfile);
            nose = CutProfile("Stapler front housing", .206f, NoseProfile);
            lever = CutProfile("Stapler lever", .151f, LeverProfile);
            for (int i = 0; i < handleRim.Length; i++)
            {
                int next = (i + 1) % handleRim.Length;
                handleRim[i] = CutProfile("Stapler handle frame", .177f,
                    new[] { HandleOutside[i], HandleOutside[next], HandleInside[next], HandleInside[i] });
            }
        }

        static Mesh CutProfile(string name, float width, Vector2[] side)
        {
            var outline = new Vector2[side.Length];
            for (int i = 0; i < side.Length; i++) outline[i] = new Vector2(-side[i].y, side[i].x);
            Mesh mesh = FactoryKitchenSlice.CreateMesh(outline, width);
            mesh.name = name;
            return mesh;
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var template = GameObject.CreatePrimitive(type);
            template.SetActive(false);
            Mesh mesh = template.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(template);
            return mesh;
        }

        static void Profile(string name, Transform parent, Vector3 position, Mesh mesh, Material body, Material edge)
        {
            var part = AddPart(name, parent, position, mesh);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            part.sharedMaterials = new[] { body, edge };
            CardboardSurface.Configure(part);
        }

        static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = AddPart(name, parent, position, cube);
            part.transform.localScale = scale;
            part.sharedMaterial = material;
            CardboardSurface.Configure(part);
        }

        static void Pin(string name, Transform parent, Vector3 position, float radius, float width, Material material)
        {
            var part = AddPart(name, parent, position, cylinder);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            part.transform.localScale = new Vector3(radius * 2f, width * .5f, radius * 2f);
            part.sharedMaterial = material;
            CardboardSurface.Configure(part);
        }

        static MeshRenderer AddPart(string name, Transform parent, Vector3 position, Mesh mesh)
        {
            var part = CardboardArt.Group(name, parent, position).gameObject;
            part.layer = parent.gameObject.layer;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            return part.AddComponent<MeshRenderer>();
        }
    }
}
