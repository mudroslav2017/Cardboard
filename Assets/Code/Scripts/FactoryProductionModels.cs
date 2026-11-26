using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    internal sealed class FactoryProductionModels
    {
        public Transform Stock { get; private set; }
        public Transform Intake { get; private set; }
        public Transform Nip { get; private set; }
        public Transform Exit { get; private set; }
        public Transform OutfeedEnd { get; private set; }
        public Transform LatheRotor { get; private set; }
        public Transform LatheSocket { get; private set; }
        public Transform LatheCutter { get; private set; }
        public Transform LatheOperator { get; private set; }
        public Transform LatheEject { get; private set; }
        public Transform LatheMotorButton { get; private set; }
        public Transform DrillSocket { get; private set; }
        public Transform DrillQuill { get; private set; }
        public Transform DrillMotorButton { get; private set; }
        public Collider StockControl { get; private set; }
        public Collider IntakeControl { get; private set; }
        public Collider ExitControl { get; private set; }
        public Collider LatheMountControl { get; private set; }
        public Collider LatheButtonControl { get; private set; }
        public Collider DrillMountControl { get; private set; }
        public Collider DrillButtonControl { get; private set; }

        const float PressCenter = 6.90f;
        const float ConveyorTop = .88f;
        const float ConveyorWidth = 2.45f;
        const float InfeedStart = 2.65f, InfeedEnd = 6.45f;
        const float OutfeedStart = 7.35f, OutfeedStop = 9.90f;
        const float LowerRollerRadius = .23f;
        const float UpperRollerRadius = .54f;
        static readonly Vector3 OperatorSide = Vector3.left;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");
        static readonly Color Ink = new Color(.14f, .16f, .17f);
        static readonly Color Frame = new Color(.68f, .73f, .76f);
        static readonly Color Deck = new Color(.49f, .55f, .60f);
        static readonly Color Moving = new Color(.32f, .39f, .44f);
        static readonly Color Marking = new Color(.92f, .94f, .93f);
        static readonly Color StartGreen = new Color(.36f, .71f, .42f);
        static readonly Color DrillOrange = new Color(.83f, .52f, .25f);

        readonly Transform root;
        readonly Material blockout;
        readonly Mesh cube, cylinder;
        readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        readonly Transform[] pressRollers = new Transform[3];
        readonly List<Transform> infeedSlats = new List<Transform>();
        readonly List<Transform> outfeedSlats = new List<Transform>();

        public FactoryProductionModels(Transform root, Material blockout)
        {
            this.root = root;
            this.blockout = blockout;
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);

            BuildStock();
            BuildPress();
            BuildLathe();
            BuildDrill();
        }

        void BuildStock()
        {
            var bin = Group("Open carton of unrolled stock", root, new Vector3(-.15f, 0f, 1.40f));
            Box("Stock base", bin, new Vector3(0, .18f, 0), new Vector3(1.18f, .36f, 1.08f), Deck, true);
            for (int sign = -1; sign <= 1; sign += 2)
                Box("Stock side", bin, new Vector3(0, .51f, sign * .505f), new Vector3(1.18f, .39f, .07f), Frame, true);
            Box("Stock back", bin, new Vector3(.55f, .48f, 0), new Vector3(.08f, .48f, 1.08f), Frame, true);
            Box("Low stock lip", bin, new Vector3(-.55f, .40f, 0), new Vector3(.08f, .28f, 1.08f), Frame, true);
            for (int i = 0; i < 3; i++)
                Box("Thick stock offcut", bin, new Vector3(.04f, .417f + i * .105f, (i - 1) * .08f),
                    new Vector3(.70f - i * .04f, .105f, .61f), Marking);
            Sign("BLANKS", bin, new Vector3(-.592f, .40f, 0), .13f, .77f);
            Stock = Group("Fresh stock grip", bin, new Vector3(-.12f, .72f, 0));
            StockControl = Hit("Take blank", bin, new Vector3(-.66f, .66f, 0), new Vector3(.40f, .52f, 1.04f));
        }

        void BuildPress()
        {
            var press = Group("Three-roller cardboard press", root, new Vector3(0, 0, PressCenter));
            Box("Press foundation", press, new Vector3(0, .17f, 0), new Vector3(3.36f, .34f, 1.76f), Deck, true);
            Box("Press drive cabinet", press, new Vector3(0, .39f, 0), new Vector3(2.88f, .49f, 1.58f), Frame, true);
            Box("Press top bridge", press, new Vector3(0, 2.43f, 0), new Vector3(3.30f, .18f, .75f), Frame);

            for (int sign = -1; sign <= 1; sign += 2)
                Box("Roller tower", press, new Vector3(sign * 1.47f, 1.29f, 0), new Vector3(.40f, 2.17f, 1.40f), Frame, true);

            pressRollers[0] = Roller(press, "Small lower infeed roller", new Vector3(0, ConveyorTop - LowerRollerRadius, -.45f), LowerRollerRadius, 2.55f);
            pressRollers[1] = Roller(press, "Small lower outfeed roller", new Vector3(0, ConveyorTop - LowerRollerRadius, .45f), LowerRollerRadius, 2.55f);
            pressRollers[2] = Roller(press, "Large centered pressure roller", new Vector3(0, 1.61f, 0), UpperRollerRadius, 2.55f);
            var rollerSupport = Group("Continuous collision deck under rollers", press, new Vector3(0, .831f, 0));
            rollerSupport.gameObject.AddComponent<BoxCollider>().size = new Vector3(ConveyorWidth, .08f, .90f);
            Box("Flat backing between lower rollers", press, new Vector3(0, .836f, 0), new Vector3(2.41f, .06f, .54f), Moving);
            Box("Press identification plate", press, new Vector3(-1.70f, 1.68f, 0), new Vector3(.06f, .33f, 1.22f), Marking);
            Sign("ROLLING PRESS", press, new Vector3(-1.738f, 1.68f, 0), .135f, 1.15f);

            Conveyor("Long wide infeed conveyor", InfeedStart, InfeedEnd, infeedSlats);
            Conveyor("Short outfeed conveyor", OutfeedStart, OutfeedStop, outfeedSlats);
            Box("First belt access step", root, new Vector3(-2.13f, .152f, 3.42f), new Vector3(.60f, .304f, .90f), Frame, true);
            Box("Second belt access step", root, new Vector3(-1.53f, .298f, 3.42f), new Vector3(.595f, .596f, .90f), Frame, true);
            Sign("FEED", root, new Vector3(-1.306f, .665f, 4.29f), .13f, .62f);
            Sign("FLAT STOCK", root, new Vector3(-1.306f, .665f, 8.80f), .12f, 1.14f);

            Intake = Group("Intake blank center", root, new Vector3(0, .985f, 3.30f));
            Nip = Group("Nip blank center", root, new Vector3(0, .934f, PressCenter));
            Exit = Group("Outfeed inspection point", root, new Vector3(0, .934f, 8.35f));
            OutfeedEnd = Group("Outfeed workpiece release", root, new Vector3(0, .934f, 9.55f));
            IntakeControl = Hit("Feed stock onto conveyor", root, new Vector3(-.92f, .98f, 3.97f), new Vector3(.89f, .64f, 2.04f));
            ExitControl = Hit("Collect stock from outfeed", root, new Vector3(-.92f, 1.02f, 8.92f), new Vector3(.90f, .60f, 1.87f));
        }

        Transform Roller(Transform parent, string name, Vector3 position, float radius, float width)
        {
            var axle = Group(name, parent, position);
            Round("Roller core", axle, Vector3.zero, Vector3.right, radius, width, Moving);
            Box("Roller motion stripe", axle, new Vector3(0, radius + .002f, 0), new Vector3(width * .94f, .009f, .027f), Marking);
            return axle;
        }

        void Conveyor(string name, float start, float end, List<Transform> slats)
        {
            var belt = Group(name, root, Vector3.zero);
            float mid = (start + end) * .5f;
            float length = end - start;
            Box("Closed belt and walkable deck", belt, new Vector3(0, ConveyorTop - .115f, mid),
                new Vector3(ConveyorWidth, .21f, length), Moving, true);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float x = sign * 1.264f;
                Box("Conveyor side", belt, new Vector3(x, .709f, mid), new Vector3(.073f, .27f, length + .10f), Frame);
                for (int leg = -1; leg <= 1; leg += 2)
                {
                    float z = mid + leg * length * .34f;
                    Box("Conveyor support", belt, new Vector3(x * .88f, .30f, z), new Vector3(.19f, .60f, .18f), Frame, true);
                }
            }
            int count = Mathf.CeilToInt(length / .26f);
            for (int i = 0; i < count; i++)
                slats.Add(Box("Moving belt slat", belt, new Vector3(0, ConveyorTop - .006f, start + (i + .5f) * length / count),
                    new Vector3(ConveyorWidth - .04f, .017f, .034f), Deck));
        }

        public void AnimatePress(float travel)
        {
            float degrees = travel * Mathf.Rad2Deg;
            pressRollers[0].localRotation = pressRollers[1].localRotation = Quaternion.Euler(degrees / LowerRollerRadius, 0, 0);
            pressRollers[2].localRotation = Quaternion.Euler(-degrees / UpperRollerRadius, 0, 0);
            MoveSlats(infeedSlats, InfeedStart, InfeedEnd, travel);
            MoveSlats(outfeedSlats, OutfeedStart, OutfeedStop, travel);
        }

        static void MoveSlats(List<Transform> slats, float start, float end, float travel)
        {
            float length = end - start;
            for (int i = 0; i < slats.Count; i++)
            {
                var slat = slats[i];
                Vector3 position = slat.localPosition;
                position.z = start + Mathf.Repeat((i + .5f) * length / slats.Count + travel, length);
                slat.localPosition = position;
            }
        }

        void BuildLathe()
        {
            var lathe = Group("Cardboard template lathe", root, new Vector3(0, 0, 13.80f));
            Box("Lathe bed", lathe, new Vector3(-.06f, .83f, 0), new Vector3(1.94f, .24f, 3.40f), Deck, true);
            for (int side = -1; side <= 1; side += 2)
                Box("Lathe pedestal", lathe, new Vector3(.02f, .40f, side * 1.06f), new Vector3(1.34f, .80f, .65f), Frame, true);
            Box("Cutter guide bed", lathe, new Vector3(-.56f, .994f, -.77f), new Vector3(.67f, .096f, 1.65f), Frame);
            for (int side = -1; side <= 1; side += 2)
                Box("Long parallel cutter guide", lathe, new Vector3(-.59f + side * .21f, 1.07f, -.77f), new Vector3(.067f, .057f, 1.62f), Moving);
            Box("Lathe headstock", lathe, new Vector3(.43f, 1.45f, .04f), new Vector3(.88f, 1.11f, 1.55f), Frame, true);
            Box("Headstock cover", lathe, new Vector3(.46f, 2.03f, .04f), new Vector3(.96f, .11f, 1.64f), Frame);
            Round("Spindle bearing housing", lathe, new Vector3(-.067f, 1.45f, 0), OperatorSide, .46f, .16f, Deck);
            LatheRotor = Group("Live facing spindle", lathe, new Vector3(-.40f, 1.45f, 0));
            LatheRotor.localRotation = Quaternion.FromToRotation(Vector3.up, OperatorSide);
            Round("Spindle", LatheRotor, new Vector3(0f, -.18f, 0f), Vector3.up, .245f, .38f, Moving);
            Round("Rotating chuck", LatheRotor, Vector3.zero, Vector3.up, .36f, .14f, Moving);
            for (int i = 0; i < 3; i++)
            {
                var jaw = Group("Chuck jaw", LatheRotor, Vector3.zero);
                jaw.localRotation = Quaternion.Euler(0, 120f * i, 0);
                Box("Chuck grip", jaw, new Vector3(0, .077f, .302f), new Vector3(.132f, .07f, .14f), Frame);
            }
            LatheSocket = Group("Blank seat on rotating chuck", LatheRotor, new Vector3(0, .125f, 0));
            LatheCutter = Group("Snapping cutter carriage", lathe, new Vector3(-.61f, 1.432f, -1.46f));
            Box("Cross slide", LatheCutter, new Vector3(.02f, -.314f, -.01f), new Vector3(.58f, .126f, .37f), Deck);
            Box("Cutter post", LatheCutter, new Vector3(.03f, -.135f, -.05f), new Vector3(.23f, .31f, .25f), Frame);
            Box("Cutter arm", LatheCutter, new Vector3(.02f, 0, .055f), new Vector3(.16f, .15f, .34f), Moving);
            var wedge = Box("Bright cutter point", LatheCutter, new Vector3(.02f, 0, .245f), new Vector3(.12f, .10f, .11f), Marking);
            wedge.localRotation = Quaternion.Euler(0, 45f, 0);

            Box("Lathe operation console", lathe, new Vector3(-.89f, 1.08f, 1.12f), new Vector3(.28f, .38f, .67f), Frame);
            LatheMotorButton = Button("Lathe motor start", lathe, new Vector3(-1.077f, 1.125f, 1.12f), StartGreen);
            Box("Lathe identification plate", lathe, new Vector3(-1.047f, .827f, .49f), new Vector3(.07f, .17f, 1.26f), Marking);
            Sign("TEMPLATE LATHE", lathe, new Vector3(-1.086f, .827f, .49f), .10f, 1.19f);
            LatheMountControl = Hit("Place template or collect cut gear", lathe, new Vector3(-.64f, 1.43f, 0), new Vector3(.56f, .88f, .99f));
            LatheButtonControl = Hit("Start lathe motor", lathe, new Vector3(-1.147f, 1.125f, 1.12f), new Vector3(.30f, .33f, .41f));
            LatheOperator = Group("Lathe operator feet", lathe, new Vector3(-2.16f, .015f, 0));
            LatheOperator.localRotation = Quaternion.Euler(0, 90f, 0);
            LatheEject = Group("Lathe ejection landing", lathe, new Vector3(-3.12f, .015f, 1.35f));
            LatheEject.localRotation = Quaternion.Euler(0, -90f, 0);
        }

        void BuildDrill()
        {
            var drill = Group("Cardboard pillar drill", root, new Vector3(0, 0, 18.20f));
            Box("Broad drill plinth", drill, new Vector3(-.12f, .17f, 0), new Vector3(2.17f, .34f, 1.90f), Deck, true);
            Box("Upright square column", drill, new Vector3(.63f, 1.23f, 0), new Vector3(.47f, 1.82f, .62f), Frame, true);
            Box("Drill table support", drill, new Vector3(.11f, .727f, 0), new Vector3(.81f, .115f, .38f), Deck);
            Box("Drill work table", drill, new Vector3(-.29f, .898f, 0), new Vector3(1.74f, .16f, 1.55f), Frame, true);
            Round("Centered drill plate", drill, new Vector3(-.48f, .98f, 0), Vector3.up, .18f, .009f, Moving);
            Box("Drill identification plate", drill, new Vector3(-1.182f, .894f, 0), new Vector3(.046f, .105f, 1.52f), Marking);
            Sign("PILLAR DRILL", drill, new Vector3(-1.209f, .896f, 0), .08f, 1.15f);
            for (int sign = -1; sign <= 1; sign += 2)
                Box("Drill table clamp", drill, new Vector3(-.52f, 1.04f, sign * .598f), new Vector3(.88f, .11f, .17f), Deck);

            Box("Drill motor head", drill, new Vector3(-.04f, 2.10f, -.04f), new Vector3(1.77f, .49f, 1.36f), Frame, true);
            Box("Overhanging motor cap", drill, new Vector3(-.07f, 2.41f, -.04f), new Vector3(1.95f, .27f, 1.54f), Deck);
            Round("Spindle collar", drill, new Vector3(-.48f, 1.89f, 0), Vector3.up, .223f, .14f, Deck);
            DrillQuill = Group("Sliding drill quill", drill, new Vector3(-.48f, 1.90f, 0));
            Round("Telescoping central shaft", DrillQuill, new Vector3(0, -.15f, 0), Vector3.up, .071f, .54f, Frame);
            Round("Drill chuck", DrillQuill, new Vector3(0, -.182f, 0), Vector3.up, .135f, .20f, Moving);
            Round("Pilot bit", DrillQuill, new Vector3(0, -.315f, 0), Vector3.up, .032f, .190f, Marking);
            DrillSocket = Group("Centered drill fixture", drill, new Vector3(-.48f, .985f, 0));

            Box("Drill switch plate", drill, new Vector3(-1.049f, 1.30f, .508f), new Vector3(.241f, .35f, .42f), Frame);
            DrillMotorButton = Button("Drill start", drill, new Vector3(-1.229f, 1.32f, .508f), DrillOrange);
            DrillMountControl = Hit("Place or collect drilled gear", drill, new Vector3(-.80f, 1.124f, -.09f), new Vector3(.74f, .42f, .76f));
            DrillButtonControl = Hit("Start drill", drill, new Vector3(-1.293f, 1.32f, .508f), new Vector3(.28f, .33f, .34f));
        }

        Transform Button(string name, Transform parent, Vector3 position, Color color)
        {
            Round("Dark button bezel", parent, position + Vector3.right * .044f, OperatorSide, .148f, .097f, Moving);
            var button = Group(name, parent, position);
            Round("Tactile button cap", button, Vector3.left * .021f, OperatorSide, .108f, .062f, color);
            return button;
        }

        Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Color color, bool solid = false)
        {
            var box = MeshPart(name, parent, position, cube, size, color, new Vector2(
                Mathf.Max(size.x, size.z), Mathf.Max(size.y, Mathf.Min(size.x, size.z))));
            if (solid) box.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
            return box;
        }

        Transform Round(string name, Transform parent, Vector3 position, Vector3 axis, float radius, float width, Color color)
        {
            var round = MeshPart(name, parent, position, cylinder, new Vector3(radius * 2f, width * .5f, radius * 2f),
                color, new Vector2(Mathf.PI * radius * 2f, width));
            round.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            return round;
        }

        Transform MeshPart(string name, Transform parent, Vector3 position, Mesh mesh, Vector3 size, Color color, Vector2 tiling)
        {
            var part = Group(name, parent, position);
            part.localScale = size;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = blockout;
            CardboardSurface.Configure(renderer);
            tint.SetColor(BaseColor, color);
            tint.SetVector(BaseMapTransform, new Vector4(tiling.x, tiling.y, .23f, .23f));
            renderer.SetPropertyBlock(tint);
            return part;
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            // Obtain Unity's shared primitive mesh without leaving temporary colliders in the line.
            var template = GameObject.CreatePrimitive(type);
            template.SetActive(false);
            var mesh = template.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(template);
            return mesh;
        }

        static Transform Group(string name, Transform parent, Vector3 position) => CardboardArt.Group(name, parent, position);

        static Collider Hit(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var collider = Group(name, parent, position).gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            return collider;
        }

        static void Sign(string caption, Transform parent, Vector3 position, float size, float width)
        {
            var label = Group(caption, parent, position);
            label.localRotation = Quaternion.Euler(0, 90f, 0);
            var ink = label.gameObject.AddComponent<TextMesh>();
            ink.font = CardboardTypography.DefaultFont;
            ink.fontSize = 40;
            ink.text = caption;
            ink.anchor = TextAnchor.MiddleCenter;
            ink.alignment = TextAlignment.Center;
            ink.color = Ink;
            ink.characterSize = size;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ink.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            float measuredWidth = renderer.localBounds.size.x;
            if (measuredWidth > width) ink.characterSize *= width / measuredWidth;
        }
    }
}
