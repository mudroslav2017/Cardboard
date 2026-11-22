using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using static Cardboard.CardboardArt;

namespace Cardboard.Editor
{
    public static class FactoryRatHuntBuilder
    {
        const string Folder = "Assets/Game_Assets/Cardboard/Sewer/RatHunt/";
        const string Palette = Folder + "Rat Hunt Props.asset";
        const string RootName = "Kitchen rat hunt";
        const int Environment = 1 << 8;
        const uint SewerLight = 1u << 3;
        static readonly string[] PipeTiles =
        {
            "01.02", "01.03", "01.04", "01.06", "02.03", "02.06", "03.02",
            "03.05", "03.07", "04.02", "04.04", "04.06", "05.04", "05.07"
        };
        static readonly Vector3[] Walls = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        [MenuItem("Cardboard/Install kitchen rat hunt")]
        public static void InstallCurrent()
        {
            var game = Object.FindAnyObjectByType<CardboardFactoryGame>();
            var sewer = game ? game.transform.Find(FactorySewerBuilder.RootName) : null;
            if (!sewer || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open FactoryShift with its sewer in Edit Mode.");
            Install(sewer);
        }

        public static void InstallBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FactoryShift.unity");
            InstallCurrent();
            EditorSceneManager.SaveScene(scene);
        }

        internal static void Install(Transform sewer)
        {
            var game = sewer.GetComponentInParent<CardboardFactoryGame>();
            var camera = game.GetComponentInChildren<CardboardCamera>().transform;
            var old = sewer.Find(RootName);
            if (old) Object.DestroyImmediate(old.gameObject);
            old = camera.Find(RootName);
            if (old) Object.DestroyImmediate(old.gameObject);
            var hunt = Group(RootName, sewer, Vector3.zero).gameObject.AddComponent<FactoryRatHunt>();
            hunt.game = game;
            game.ratHunt = hunt;
            var props = new Props();
            hunt.targets = BuildPipes(hunt.transform, sewer, props);
            BuildEquipment(hunt, Group(RootName, camera, Vector3.zero), props);
            BuildDelivery(hunt.transform, game, props);
            hunt.ResetHunt();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        static FactoryRatTarget[] BuildPipes(Transform root, Transform sewer, Props props)
        {
            var tiles = sewer.Find("Tunnel tiles").Cast<Transform>().ToDictionary(tile => tile.name.Substring(0, 5));
            float floor = sewer.Find("Maintenance hatches/A • sewer landing").position.y - .18f;
            var targets = new List<FactoryRatTarget>();
            Physics.SyncTransforms();
            for (int i = 0; i < PipeTiles.Length; i++)
            {
                Vector3 center = tiles[PipeTiles[i]].position;
                center.y = floor;
                if (!TryFindPipe(center, i, out var mouth, out var inward, out var forage, out var waste))
                    throw new InvalidOperationException("No clear floor for rat pipe in sewer tile " + PipeTiles[i]);
                var pipe = Group("Rat pipe " + (i + 1), root, mouth);
                pipe.rotation = Quaternion.LookRotation(inward);
                Draw(pipe, props.pipe, SewerLight, props.iron, props.rim, props.shadow);
                var rear = Primitive("Dark pipe interior", pipe, PrimitiveType.Cylinder, new Vector3(0, .33f, -.50f),
                    new Vector3(.71f, .014f, .71f), props.shadow, SewerLight);
                rear.localRotation = Quaternion.Euler(90, 0, 0);
                Primitive("Pipe marker lens", pipe, PrimitiveType.Cube, new Vector3(0, .91f, -.41f),
                    new Vector3(.20f, .095f, .13f), props.lens, SewerLight);
                var light = Group("Pipe marker lamp", pipe, new Vector3(0, .86f, -.07f)).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1, .78f, .49f);
                light.range = 3.8f;
                light.intensity = 2.1f;
                light.GetUniversalAdditionalLightData().renderingLayers = SewerLight;

                var rat = Target(root, "Live rat", FactoryRatFood.LiveRat, mouth, new Vector3(.36f, .30f, .71f));
                rat.den = mouth - inward * .43f;
                rat.mouth = mouth;
                rat.forage = forage;
                rat.firstEmergence = .22f + i % 4 * .35f;
                rat.ratModel = Group("Scurry pivot", rat.transform, Vector3.zero);
                Rat(rat.ratModel, Vector3.zero, 1.98f, Quaternion.identity, props, false);
                targets.Add(rat);

                if (i % 2 == 0)
                {
                    var spoiled = Target(root, "Rotten rat", FactoryRatFood.RottenRat, waste, new Vector3(.40f, .23f, .71f));
                    spoiled.transform.rotation = Quaternion.LookRotation(inward) * Quaternion.Euler(0, 37 + i * 17, 0);
                    Rat(spoiled.transform, new Vector3(0, -.035f, 0), 1.98f, Quaternion.Euler(0, 0, 103), props, true);
                    Primitive("Mold on flank", spoiled.transform, PrimitiveType.Sphere, new Vector3(.055f, .092f, -.04f),
                        new Vector3(.105f, .047f, .14f), props.mold, SewerLight);
                    targets.Add(spoiled);
                }
                else
                {
                    var droppings = Target(root, "Droppings", FactoryRatFood.Droppings, waste, new Vector3(.33f, .20f, .44f));
                    for (int pellet = 0; pellet < 4; pellet++)
                    {
                        var lump = Primitive("Pellet", droppings.transform, PrimitiveType.Sphere,
                            new Vector3(pellet % 2 * .13f - .07f, .037f, pellet * .085f - .13f),
                            new Vector3(.085f, .076f, .15f - pellet * .013f), props.waste, SewerLight);
                        lump.localRotation = Quaternion.Euler(0, pellet * 73 + i * 29, 0);
                    }
                    targets.Add(droppings);
                }
            }
            return targets.ToArray();
        }

        static bool TryFindPipe(Vector3 center, int index, out Vector3 mouth, out Vector3 inward,
            out Vector3 forage, out Vector3 waste)
        {
            for (int i = 0; i < Walls.Length; i++)
            {
                Vector3 wall = Walls[(i + index) % Walls.Length];
                inward = -wall;
                Vector3 tangent = Vector3.Cross(Vector3.up, inward);
                foreach (float offset in new[] { 0f, 2.3f, -2.3f })
                {
                    Vector3 rayStart = center + tangent * offset + Vector3.up * .45f;
                    if (!Physics.Raycast(rayStart, wall, out var boundary, 8.1f, Environment, QueryTriggerInteraction.Ignore) ||
                        boundary.distance < 2.6f || Vector3.Dot(boundary.normal, inward) < .9f)
                        continue;
                    if (!FloorAt(boundary.point + inward * .85f, out mouth) ||
                        Mathf.Abs(mouth.y - center.y) > .16f) continue;
                    foreach (int side in new[] { 1, -1 })
                    {
                        Vector3 bank = inward * 1.2f + tangent * (2.25f * side);
                        if (FloorAt(mouth + bank, out forage) &&
                            FloorAt(mouth + inward * 1.4f - tangent * (1.35f * side), out waste) &&
                            ClearBank(mouth, forage) && ClearBank(mouth, waste) &&
                            FloorAt(mouth - inward * .46f, out var den) && Mathf.Abs(den.y - mouth.y) < .055f)
                            return true;
                    }
                }
            }
            mouth = inward = forage = waste = default;
            return false;
        }

        static bool FloorAt(Vector3 point, out Vector3 floor)
        {
            bool hit = Physics.Raycast(point + Vector3.up * .48f, Vector3.down, out var ray, 1.15f, Environment,
                QueryTriggerInteraction.Ignore);
            floor = ray.point + Vector3.up * .012f;
            return hit && ray.normal.y > .9f;
        }

        static bool ClearBank(Vector3 start, Vector3 end)
        {
            Vector3 lift = Vector3.up * .12f;
            if (Physics.Linecast(start + lift, end + lift, Environment, QueryTriggerInteraction.Ignore)) return false;
            for (int i = 1; i <= 5; i++)
            {
                Vector3 step = Vector3.Lerp(start, end, i / 5f);
                if (!FloorAt(step, out var floor) || Mathf.Abs(start.y - floor.y) > .055f) return false;
            }
            return true;
        }

        static FactoryRatTarget Target(Transform parent, string name, FactoryRatFood food, Vector3 point, Vector3 size)
        {
            var target = Group(name, parent, point);
            var hit = target.gameObject.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = Vector3.up * size.y * .5f;
            hit.size = size;
            Action(target, FactoryAction.RatFood, "E - GRAB");
            var collectable = target.gameObject.AddComponent<FactoryRatTarget>();
            collectable.food = food;
            return collectable;
        }

        static void BuildEquipment(FactoryRatHunt hunt, Transform view, Props props)
        {
            hunt.grabber = Group("Right hand • claw grabber", view, new Vector3(.31f, -.346f, .54f));
            var model = Object.Instantiate(props.claw, hunt.grabber, false).transform;
            model.localScale = Vector3.one * .031f;
            model.localRotation = Quaternion.LookRotation(new Vector3(-.23f, .11f, 1)) * Quaternion.Euler(0, -90, 0);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                bool jaw = renderer.name.StartsWith("Hvatalka");
                if (jaw)
                {
                    bool left = renderer.name.Contains(" l.");
                    var hinge = Group(renderer.name + " hinge", model, new Vector3(22.2f, 0, left ? 1.45f : -1.45f));
                    renderer.transform.SetParent(hinge, true);
                    if (left) hunt.leftJaw = hinge;
                    else hunt.rightJaw = hinge;
                }
                renderer.sharedMaterial = jaw ? props.jaws : props.handle;
                ViewRenderer(renderer);
            }
            if (!hunt.leftJaw || !hunt.rightJaw) throw new InvalidOperationException("Grabber jaw meshes are missing.");
            Hand(hunt.grabber, new Vector3(.007f, -.03f, .089f), 1, props);

            hunt.basket = Group("Left hand • mesh basket", view, new Vector3(-.31f, -.327f, .76f));
            hunt.basket.localRotation = Quaternion.Euler(12, -8, -5);
            Draw(hunt.basket, props.basket, 1, props.wire);
            ViewRenderer(hunt.basket.GetComponent<MeshRenderer>());
            Hand(hunt.basket, new Vector3(-.147f, .005f, -.13f), -1, props);
            hunt.basketContents = new GameObject[3];
            for (int i = 0; i < hunt.basketContents.Length; i++)
            {
                var rat = Rat(hunt.basket, new Vector3(-.092f + i * .09f, -.073f + i * .017f, .028f - i * .023f),
                    .65f, Quaternion.Euler(2, 69 + i * 120, 12), props, false, true);
                hunt.basketContents[i] = rat.gameObject;
            }
        }

        static void Hand(Transform parent, Vector3 grip, int side, Props props)
        {
            var hand = Group(side == 1 ? "Right glove" : "Left glove", parent, grip);
            var sleeve = Primitive("Sleeve", hand, PrimitiveType.Cube, new Vector3(.046f * side, -.068f, -.076f),
                new Vector3(.102f, .115f, .23f), props.sleeve);
            sleeve.localRotation = Quaternion.Euler(-22, -18 * side, -8 * side);
            Primitive("Cuff", hand, PrimitiveType.Cube, new Vector3(.021f * side, -.026f, -.026f),
                new Vector3(.108f, .10f, .043f), props.cuff);
            Primitive("Palm", hand, PrimitiveType.Cube, Vector3.zero, new Vector3(.092f, .076f, .101f), props.glove);
            for (int i = 0; i < 3; i++)
            {
                var finger = Primitive("Curled finger", hand, PrimitiveType.Cube, new Vector3(-.035f * side, .026f, i * .029f - .027f),
                    new Vector3(.047f, .051f, .024f), props.glove);
                finger.localRotation = Quaternion.Euler(0, 0, -35 * side);
            }
            foreach (var renderer in hand.GetComponentsInChildren<MeshRenderer>()) ViewRenderer(renderer);
        }

        static Transform Rat(Transform parent, Vector3 position, float scale, Quaternion rotation, Props props, bool spoiled, bool view = false)
        {
            var holder = Group("Rat model", parent, position);
            holder.localRotation = rotation;
            holder.localScale = Vector3.one * scale;
            var model = Object.Instantiate(props.rat, holder, false);
            model.transform.localPosition += new Vector3(0, .035f, -.043f);
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterials = view ? new[] { props.basketTail, props.basketFur } :
                spoiled ? new[] { props.mold, props.spoiled } : new[] { props.tail, props.fur };
            if (view) ViewRenderer(renderer);
            else renderer.renderingLayerMask = SewerLight;
            return holder;
        }

        static void BuildDelivery(Transform root, CardboardFactoryGame game, Props props)
        {
            var servingCounter = game.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Lunch serving counter"));
            Bounds bounds = servingCounter.GetComponent<Renderer>().bounds;
            var point = Group("Kitchen food intake", root,
                new Vector3(bounds.center.x + .76f, bounds.max.y, bounds.max.z - .37f));
            Box("Intake tray", point, new Vector3(0, .05f, 0), new Vector3(.74f, .10f, .51f), props.iron);
            Box("Delivery label", point, new Vector3(0, .22f, .02f), new Vector3(.71f, .27f, .065f), props.rim);
            var sign = Group("Rat drop lettering", point, new Vector3(0, .22f, .058f));
            sign.localRotation = Quaternion.Euler(0, 180, 0);
            var caption = sign.gameObject.AddComponent<TextMesh>();
            caption.font = CardboardTypography.DefaultFont;
            caption.text = "RAT DROP";
            caption.fontSize = 32;
            caption.characterSize = .034f;
            caption.anchor = TextAnchor.MiddleCenter;
            caption.color = Hex("28231A");
            var ink = game.GetComponentInChildren<CardboardWorldLabel>().ink;
            sign.gameObject.AddComponent<CardboardWorldLabel>().ink = ink;
            sign.GetComponent<CardboardWorldLabel>().Refresh();
            var trigger = point.gameObject.AddComponent<BoxCollider>();
            trigger.center = Vector3.up * .20f;
            trigger.size = new Vector3(.91f, .42f, .57f);
            trigger.isTrigger = true;
            Action(point, FactoryAction.RatDelivery, "E - DELIVER FOOD");
        }

        static void Action(Transform root, FactoryAction action, string prompt)
        {
            var interactable = root.gameObject.AddComponent<FactoryInteractable>();
            interactable.action = action;
            interactable.prompt = prompt;
        }

        static Transform Primitive(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale,
            Material material, uint lights = 1)
        {
            var piece = GameObject.CreatePrimitive(shape).transform;
            piece.name = name;
            piece.SetParent(parent, false);
            piece.localPosition = position;
            piece.localScale = scale;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderingLayerMask = lights;
            return piece;
        }

        static void Draw(Transform node, Mesh mesh, uint lights, params Material[] materials)
        {
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.renderingLayerMask = lights;
        }

        static void ViewRenderer(MeshRenderer renderer)
        {
            renderer.gameObject.layer = CardboardMarkerHand.HandLayer;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
        }

        sealed class Props
        {
            public readonly GameObject rat = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Ratv2.fbx");
            public readonly GameObject claw = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Hvatalka.fbx");
            public readonly Mesh pipe, basket;
            public readonly Material iron, rim, shadow, lens, tail, fur, mold, spoiled, waste, basketTail, basketFur;
            public readonly Material handle, jaws, wire, glove, cuff, sleeve;

            public Props()
            {
                if (!rat || !claw) throw new InvalidOperationException("Ratv2.fbx and Hvatalka.fbx are required.");
                pipe = Save(Pipe(), "Rat Hunt Props");
                basket = Save(Basket(), "Wire mesh basket");
                iron = Lit("Iron", "536C68", "111C1A", .45f);
                rim = Lit("Ochre rim", "C9A756", "30200D", .28f);
                shadow = Lit("Pipe interior", "17201F", "080B0A", 0);
                lens = Lit("Amber marker lens", "D8B158", "FFD177", .2f);
                tail = Lit("Rat tail", "CB8C77", "8A5D50", .1f);
                fur = Lit("Rat fur", "BBB0AE", "6B6360", .05f);
                mold = Lit("Mold", "BAC66B", "93974F", .08f);
                spoiled = Lit("Spoiled fur", "8A9277", "596148", .05f);
                waste = Lit("Droppings", "75513B", "65503A", .18f);
                basketTail = View("Basket rat tail", "C18972");
                basketFur = View("Basket rat fur", "999697");
                handle = View("Grabber handle", "E1A84D");
                jaws = View("Grabber claws", "C4D1CC");
                wire = View("Basket wire", "AFBCB1");
                glove = View("Kraft gloves", "DAB081");
                cuff = View("Cuff", "2A524D");
                sleeve = View("Work sleeve", "538277");
            }

            static T Save<T>(T item, string name) where T : Object
            {
                item.name = name;
                var main = AssetDatabase.LoadMainAssetAtPath(Palette);
                var old = main ? AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<T>().FirstOrDefault(asset => asset.name == name) : null;
                if (old)
                {
                    EditorUtility.CopySerialized(item, old);
                    EditorUtility.SetDirty(old);
                    Object.DestroyImmediate(item);
                    return old;
                }
                if (main) AssetDatabase.AddObjectToAsset(item, Palette);
                else AssetDatabase.CreateAsset(item, Palette);
                return item;
            }

            static Material Lit(string name, string color, string emission, float smoothness)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Hex(color));
                material.SetColor("_EmissionColor", Hex(emission) * .56f);
                material.SetFloat("_Smoothness", smoothness);
                material.EnableKeyword("_EMISSION");
                return Save(material, name);
            }

            static Material View(string name, string color)
            {
                var material = new Material(Shader.Find(CardboardMarkerHand.ShaderName));
                material.SetColor("_BaseColor", Hex(color));
                return Save(material, name);
            }
        }

        static Mesh Pipe()
        {
            const int sides = 16;
            var profile = new (float radius, float z, int material)[]
            {
                (.405f, -.69f, 0), (.405f, -.105f, 0), (.455f, -.105f, 1),
                (.455f, .012f, 1), (.334f, .012f, 1), (.334f, -.56f, 2)
            };
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int ring = 0; ring < profile.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float theta = 2 * Mathf.PI * side / sides;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * profile[ring].radius,
                        .33f + Mathf.Sin(theta) * profile[ring].radius, profile[ring].z));
                    if (ring == 0) continue;
                    int a = (ring - 1) * sides + side, b = (ring - 1) * sides + (side + 1) % sides;
                    triangles[profile[ring].material].AddRange(new[] { a, b, b + sides, a, b + sides, a + sides });
                }
            }
            var mesh = new Mesh { subMeshCount = triangles.Length };
            mesh.SetVertices(vertices);
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh Basket()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Wire(Vector3 a, Vector3 b, float width)
            {
                Vector3 delta = b - a;
                if (delta.sqrMagnitude < .000001f) return;
                const int facets = 5;
                var rotation = Quaternion.FromToRotation(Vector3.forward, delta);
                int start = vertices.Count;
                for (int side = 0; side < facets; side++)
                {
                    float angle = 2 * Mathf.PI * side / facets;
                    Vector3 edge = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (.5f * width);
                    vertices.Add(a + edge);
                    vertices.Add(b + edge);
                    int first = start + 2 * side, next = start + 2 * ((side + 1) % facets);
                    triangles.AddRange(new[] { first, next, next + 1, first, next + 1, first + 1 });
                }
            }

            const int sides = 24;
            Vector3 Point(int side, int row)
            {
                float t = row / 6f, angle = side * 2 * Mathf.PI / sides;
                return new Vector3(Mathf.Cos(angle) * Mathf.Lerp(.142f, .211f, t),
                    Mathf.Lerp(-.142f, .064f, t), Mathf.Sin(angle) * Mathf.Lerp(.115f, .158f, t));
            }
            for (int side = 0; side < sides; side++)
            {
                Wire(Point(side, 0), Point(side, 6), .009f);
                if (side < sides / 2) Wire(Point(side, 0), Point(-side, 0), .006f);
                for (int row = 0; row <= 6; row++)
                    Wire(Point(side, row), Point(side + 1, row), row == 0 || row == 6 ? .017f : .007f);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector3 Arch(int n)
                {
                    float angle = n * Mathf.PI / 10;
                    return new Vector3(Mathf.Cos(angle) * .207f, .063f + Mathf.Sin(angle) * .124f, -.036f);
                }
                Wire(Arch(i), Arch(i + 1), .021f);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
