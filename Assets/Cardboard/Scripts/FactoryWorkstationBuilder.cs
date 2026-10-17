using System;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.FactoryGeometry;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cardboard
{
    public static class FactoryWorkstationBuilder
    {
#if UNITY_EDITOR
        const string Models = "Assets/Game_Assets/Cardboard/";
        static Transform Model(string name, string path, Transform parent, Material material, float yaw = 90)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + path);
            if (!asset) throw new InvalidOperationException("Missing workstation model: " + path);
            var pose = Group(name, parent, Vector3.zero); pose.localRotation = Quaternion.Euler(0, yaw, 0);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, pose);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.enabled = false;
            return pose;
        }
        static Bounds BoundsIn(Transform model, Transform parent)
        {
            bool first = true; Bounds bounds = default;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                Bounds local = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = parent.InverseTransformPoint(renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        static Transform Pivot(Transform model, Transform station, Vector3 position, string name)
        {
            var pivot = Group(name, station, position); model.SetParent(pivot, true); return pivot;
        }
        static FactoryStationControl Control(Transform parent, FactoryWorkstation station, StationPart part, Vector3 position, Vector3 size, int number = 0)
        {
            var target = Group("Interact " + part + (number > 0 ? " " + number : ""), parent, position);
            target.gameObject.AddComponent<BoxCollider>().size = size;
            Interaction(target, FactoryAction.Station, "");
            var control = target.gameObject.AddComponent<FactoryStationControl>(); control.station = station; control.part = part; control.number = number;
            return control;
        }
        static void Number(Transform parent, Vector3 position, int number, float size = .13f)
        {
            var face = Group("Number " + number, parent, position); face.localRotation = Quaternion.Euler(0, 180, 0);
            WorldLabel(number.ToString(), face, Vector3.zero, size, .5f);
        }
#endif
        public static FactoryWorkstation Build(Transform parent, CardboardFactoryGame game)
        {
#if UNITY_EDITOR
            // Model poses come from Untitled.blend. FBX files retain their own origins;
            // pose wrappers align them without changing the meshes or the guard animation.
            var root = Group("Player workstation • imported models", parent, new Vector3(-6, 0, .5f));
            var station = root.gameObject.AddComponent<FactoryWorkstation>();
            root.gameObject.AddComponent<FactoryWorkbenchSound>();
            Model("Table", "Table/Model/Table.fbx", root, Teal);
            Box("Workstation backboard",root,new Vector3(.85f,1.75f,.85f),new Vector3(4.02f,2,.072f),Teal);
            var conveyor = Model("Static conveyor", "Conveer/Model/Conveer.fbx", root, Edge);
            conveyor.localPosition += new Vector3(-2.14f, 1.015f, 0) - BoundsIn(conveyor, root).center;
            var pipe = Model("Material pipe", "Pipe/Model/Pipe.fbx", root, Teal, -90);
            pipe.localPosition += new Vector3(-3.385f, 3.458f, 0) - BoundsIn(pipe, root).center;
            Model("Under press", "Under_Press/Model/Under_Press.fbx", root, Edge);
            var press = Model("Moving press", "Press/Model/Press.fbx", root, Cream);
            press.localScale = new Vector3(1, .65f, 1);
            var pressBounds = BoundsIn(press, root);
            press.localPosition += Vector3.up * (1.65f - pressBounds.min.y); station.press = press;

            var feed = Model("Feed lever model", "lever/lever.fbx", root, Gold);
            station.feedLever = Pivot(feed, root, new Vector3(-2.706f, 1.028f, -.521f), "Feed lever pivot");
            Control(station.feedLever, station, StationPart.Feed, new Vector3(0, .18f, -.32f), new Vector3(.48f, .65f, .8f));
            var clamp = Model("Second lever model", "lever/lever.fbx", root, Coral);
            station.pressLever = Pivot(clamp, root, new Vector3(-2.706f, 1.028f, -.521f), "Press lever pivot");
            station.pressLever.localPosition = new Vector3(.68f, 1.028f, -.521f);
            Control(station.pressLever, station, StationPart.Press, new Vector3(0, .18f, -.32f), new Vector3(.48f, .65f, .8f));

            var dial = Model("Krutilka model", "Krutilka/Model/Krutilka.fbx", root, Gold);
            station.selector = Pivot(dial, root, BoundsIn(dial, root).center, "Gear type selector pivot");
            station.selector.localPosition = new Vector3(-1.13f, 2.016f, .76f);
            Control(station.selector, station, StationPart.Selector, Vector3.zero, new Vector3(.60f, .65f, .26f));
            var selectorFace = Group("Selected type", root, new Vector3(-1.13f, 2.48f, .68f)); selectorFace.localRotation = Quaternion.Euler(0, 180, 0);
            station.selectorNumber = Label("1", selectorFace, Vector3.zero, .16f).GetComponent<TextMesh>();
            for (int i = 0; i < 3; i++) Number(root, new Vector3(-1.45f + i * .32f, 1.55f, .68f), i + 1, .07f);

            var guard = Model("Animated Zabralo", "Zabralo/Zabralo.fbx", root, Coral);
            station.guardModel = guard.GetChild(0);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Models + "Zabralo/Zabralo.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) { station.guardClip = clip; break; }
            if (!station.guardClip) throw new InvalidOperationException("Zabralo must have its imported animation.");
            station.guardHit=Control(root, station, StationPart.Guard, new Vector3(-.05f, 1.18f, -.48f), new Vector3(.83f, 1.1f, .25f)).GetComponent<BoxCollider>();

            var wheelAssembly = Model("Shturval assembly", "Shturval/Shturval.fbx", root, Edge);
            Transform rim = null;
            foreach (var renderer in wheelAssembly.GetComponentsInChildren<Renderer>()) if (renderer.name == "Torus") rim = renderer.transform;
            if (!rim) throw new InvalidOperationException("Shturval rim is missing.");
            station.wheel = Group("Rotating wheel and crank", root, new Vector3(2.049f, .798f, -.334f)); rim.SetParent(station.wheel, true);
            Control(root, station, StationPart.Wheel, new Vector3(2.049f, .85f, -.42f), new Vector3(1.36f, 1.45f, .36f));
            // A narrow invisible collider keeps the table solid without blocking the controls.
            var tableBody = Group("Workbench collision", root, new Vector3(0, .84f, .12f)); tableBody.gameObject.layer = 8;
            tableBody.gameObject.AddComponent<BoxCollider>().size = new Vector3(2.30f, .08f, .85f);

            station.sheet = Box("Moving cardboard blank", root, station.dropPosition, new Vector3(.64f, .035f, .64f), Kraft);
            station.cutPart = Group("Cut gear under the press", root, station.cuttingPosition); station.cutPart.localScale = Vector3.one * .30f;
            Control(root, station, StationPart.Output, new Vector3(-.08f, .98f, -.12f), new Vector3(.65f, .22f, .72f));
            station.pumpPlate = Box("Jump pressure plate", root, new Vector3(0, -.08f, -1.5f), new Vector3(1.6f, .12f, 1.2f), Gold);
            for (int i = -3; i <= 3; i++) Box("Plate rib", station.pumpPlate, new Vector3(i * .12f, .52f, 0), new Vector3(.025f, .03f, .8f), Edge);
            var gauge = Group("Pressure gauge", root, new Vector3(-.65f, 1.55f, -.53f));
            Disc("Pressure dial", gauge, Vector3.zero, .16f, .035f, Cream);
            station.pressureNeedle = Group("Pressure needle", gauge, Vector3.zero);
            Stroke("Needle", station.pressureNeedle, new Vector3(0, 0, -.025f), new Vector3(0, .13f, -.025f), .018f, Coral);
            station.pressureLamps = new Renderer[4]; station.lampOff = Ink; station.lampOn = Gold;
            for (int i = 0; i < 4; i++) station.pressureLamps[i] = Box("Pressure step " + (i + 1), root, new Vector3(-.85f + i * .14f, 1.30f, -.53f), new Vector3(.10f, .08f, .03f), Ink).GetComponent<Renderer>();
            Box("Cut progress track", root, new Vector3(2.049f, 1.68f, -.46f), new Vector3(1.15f, .09f, .035f), Ink);
            station.progressFill = Box("Cut progress", root, new Vector3(2.049f, 1.68f, -.485f), new Vector3(1.10f, .05f, .025f), Gold);

            station.baths = new Transform[3]; game.finishMaterials = new[] {Teal, Gold, Coral};
            for (int i = 0; i < 3; i++)
            {
                var bath = Group("Finishing bath " + (i + 1), root, new Vector3(3.45f + i * 1.02f, 0, 0)); station.baths[i] = bath;
                Solid("Tub base", bath, new Vector3(0, .10f, 0), new Vector3(.88f, .14f, 1.1f), Teal);
                Solid("Tub rear", bath, new Vector3(0, .45f, .52f), new Vector3(.88f, .7f, .07f), Teal);
                Solid("Tub front", bath, new Vector3(0, .40f, -.52f), new Vector3(.88f, .60f, .07f), Teal);
                for (int side = -1; side <= 1; side += 2) Solid("Tub side", bath, new Vector3(side * .42f, .45f, 0), new Vector3(.07f, .7f, 1.1f), Teal);
                Box("Finishing liquid", bath, new Vector3(0, .57f, 0), new Vector3(.75f, .025f, .95f), game.finishMaterials[i]);
                Number(bath, new Vector3(0, .42f, -.565f), i + 1, .20f);
                Control(bath, station, StationPart.Bath, new Vector3(0, .55f, -.59f), new Vector3(.90f, .9f, .10f), i + 1);
            }
            root.localRotation = Quaternion.Euler(0, 180, 0);
            station.ResetMachine(); return station;
#else
            throw new InvalidOperationException("Build the workstation in the Unity editor and save the scene before building the player.");
#endif
        }
    }
}
