using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using static Cardboard.CardboardArt;

namespace Cardboard.Editor
{
    public static class FactoryKitchenInstaller
    {
        const string ScenePath = "Assets/Scenes/FactoryShift.unity";
        const string TableTextures = "Assets/Game_Assets/Cardboard/Table/Texture/";

        public static void InstallBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Install();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Cardboard/Install canteen kitchen")]
        static void Install()
        {
            var game = Object.FindAnyObjectByType<CardboardFactoryGame>();
            if (!game || !game.lunch || !game.ratHunt || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open FactoryShift with its canteen and rat hunt in Edit Mode.");
            if (game.kitchen)
            {
                Object.DestroyImmediate(game.kitchen.traySoup);
                Object.DestroyImmediate(game.kitchen.gameObject);
            }
            var root = Node(game.transform, "Canteen kitchen", new Vector3(-20.9f, 0, -23f));
            var kitchen = root.gameObject.AddComponent<FactoryKitchen>();
            game.kitchen = kitchen;
            kitchen.game = game;

            var wood = Paint("Kraft", "B99971", "Table_Top.png");
            var steel = Paint("Steel", "424E48");
            var tin = Paint("Tin", "C5BBA4");
            var trim = Paint("Edge", "5A3626");
            var grain = Paint("Cabinet", "B79A77", "Table_Top.png");
            grain.SetTextureOffset("_BaseMap", new Vector2(.043f, .075f));
            grain.SetTextureScale("_BaseMap", new Vector2(.353f, .824f));
            EditorUtility.SetDirty(grain);
            var enamel = Paint("Enamel", "779084");
            var peel = Paint("Peel", "99623A");
            var stoveGlow = Paint("Coal", "E98640");
            kitchen.potatoPaper = Paint("Potato", "E9C785");
            var cutEdge = Paint("CutEdge", "C28A53", "Table_Top.png");
            var dust = Paint("Dust", "54463A");
            var soup = Paint("Soup", "B6783F");

            BuildBench(root, wood, grain, trim, enamel, tin);

            var rat = Button(root, kitchen, KitchenAction.Rat, "Rat stock", new Vector3(-2.07f, 1.014f, -.06f),
                new Vector3(.60f, .12f, .65f), grain);
            CrateRim(rat, .60f, .65f, grain, trim);
            var ratSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game_Assets/Cardboard/Sewer/RatHunt/Ratv2.fbx");
            var crateRat = (GameObject)PrefabUtility.InstantiatePrefab(ratSource, rat);
            kitchen.ratModel = BakeRat(crateRat, cutEdge, out var ratMaterials);
            kitchen.ratMaterials = ratMaterials;
            FitProp(crateRat, rat.TransformPoint(new Vector3(0, .081f, 0)), .315f);
            kitchen.potatoModel = BuildPotatoMesh();
            kitchen.potatoMaterials = Enumerable.Repeat(peel, kitchen.potatoModel.subMeshCount).ToArray();
            kitchen.potatoMaterials[kitchen.potatoMaterials.Length - 1] = cutEdge;
            var potatoes = Button(root, kitchen, KitchenAction.Potato, "Potato box", new Vector3(-2.86f, .38f, -.16f),
                new Vector3(.61f, .26f, .66f), grain);
            CrateRim(potatoes, .61f, .66f, grain, trim);
            Box(root, "Potato crate stand", new Vector3(-2.86f, .03f, -.16f), new Vector3(.70f, .45f, .70f), trim);
            for (int i = 0; i < 3; i++)
            {
                var model = Node(potatoes, "Potato", new Vector3((i - 1) * .145f, .22f, i % 2 * .09f));
                model.localScale = Vector3.one * .49f;
                model.localRotation = Quaternion.Euler(14 * i, 65 + i * 33, 0);
                model.gameObject.AddComponent<MeshFilter>().sharedMesh = kitchen.potatoModel;
                model.gameObject.AddComponent<MeshRenderer>().sharedMaterials = kitchen.potatoMaterials;
            }

            var boardHome = Node(root, "Board stand", new Vector3(-1.22f, .98f, -.15f));
            kitchen.board = Tool(root, kitchen, boardHome, false);
            var cuttingSurface = Box(kitchen.board.transform, "Chopping surface", new Vector3(0, -.008f, 0), new Vector3(.74f, .043f, .58f), wood);
            cuttingSurface.GetComponent<Renderer>().enabled = false;
            Sheet(kitchen.board.transform, "Banded chopping surface", "KitchenChoppingSurface", new Vector3(0, -.008f, 0), .74f, .58f, .043f, .044f, wood, trim);
            foreach (float x in new[] { -.354f, .354f })
                Box(kitchen.board.transform, "Board edge", new Vector3(x, .032f, 0), new Vector3(.025f, .033f, .58f), trim);
            Box(kitchen.board.transform, "Board grip", new Vector3(0, -.01f, -.355f), new Vector3(.16f, .041f, .17f), trim);
            Button(root, kitchen, KitchenAction.Prep, "Prep dock", new Vector3(-1.23f, .84f, -.565f),
                new Vector3(.53f, .14f, .058f), enamel);

            kitchen.knife = Node(root, "Kitchen knife", new Vector3(-1.31f, 1.045f, .37f));
            kitchen.knife.localRotation = Quaternion.Euler(0, 90, 0);
            BuildKnife(kitchen, tin, steel, trim);
            var knifeRack = Button(root, kitchen, KitchenAction.Knife, "Knife rack", new Vector3(-1.33f, .971f, .37f),
                new Vector3(.79f, .053f, .137f), trim);
            GripVolume(knifeRack, new Vector3(-.05f, .103f, 0), new Vector3(.94f, .15f, .17f));
            kitchen.peelingStand = Node(root, "Potato peeling center", new Vector3(-.651f, 1.166f, -.16f));
            var peelRest = Button(root, kitchen, KitchenAction.Peeler, "Peeling stand", new Vector3(-.651f, 1.002f, -.16f),
                new Vector3(.38f, .091f, .35f), enamel);
            kitchen.peeler = Node(root, "Kitchen peeler", new Vector3(-.65f, .976f, .265f));
            Box(kitchen.peeler, "Peeler handle", new Vector3(0, .04f, -.046f), new Vector3(.048f, .048f, .12f), trim, false);
            Box(kitchen.peeler, "Peeler blade", new Vector3(0, .025f, .035f), new Vector3(.14f, .018f, .043f), tin, false);
            GripVolume(peelRest, new Vector3(0, .02f, .425f), new Vector3(.207f, .102f, .234f));
            kitchen.knifeDashes = new Transform[13];
            var guide = PaintGuide();
            for (int i = 0; i < kitchen.knifeDashes.Length; i++)
            {
                var dash = Box(root, "Projected knife dash", Vector3.zero, new Vector3(.008f, .002f, .030f), guide, false);
                dash.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                kitchen.knifeDashes[i] = dash;
                kitchen.knifeDashes[i].gameObject.SetActive(false);
            }

            var burnerBody = Box(root, "Stove", new Vector3(.05f, 1.035f, -.04f), new Vector3(.99f, .17f, .88f), steel);
            var panHome = Node(root, "Pan rest", new Vector3(.05f, 1.151f, .02f));
            var lamp = Shape(root, PrimitiveType.Cylinder, "Coals", new Vector3(.05f, 1.126f, .02f),
                new Vector3(.57f, .008f, .57f), stoveGlow, false);
            kitchen.burnerLamp = lamp.GetComponent<Renderer>();
            kitchen.burnerLamp.enabled = false;
            kitchen.pan = Tool(root, kitchen, panHome, true);
            Vessel(kitchen.pan.transform, .322f, .11f, steel, tin);
            Box(kitchen.pan.transform, "Pan handle", new Vector3(0, .075f, -.445f), new Vector3(.10f, .06f, .31f), trim);
            var switchBase = Button(root, kitchen, KitchenAction.Burner, "Burner switch", new Vector3(-.16f, 1.018f, -.512f),
                new Vector3(.26f, .12f, .065f), tin);
            Shape(switchBase, PrimitiveType.Cylinder, "Ridged heat knob", new Vector3(0, 0, -.052f),
                new Vector3(.087f, .034f, .087f), trim, false).localRotation = Quaternion.Euler(90, 0, 0);
            Button(root, kitchen, KitchenAction.Spatula, "Spatula", new Vector3(.36f, 1.024f, -.507f),
                new Vector3(.23f, .12f, .065f), enamel);
            Box(root, "Spatula handle", new Vector3(.55f, .99f, -.30f), new Vector3(.055f, .038f, .21f), trim, false);
            Box(root, "Spatula head", new Vector3(.55f, .979f, -.135f), new Vector3(.12f, .018f, .13f), tin, false);
            kitchen.scorchSmoke = BuildSmoke(root, panHome.localPosition + Vector3.up * .11f);
            kitchen.sizzle = burnerBody.gameObject.AddComponent<AudioSource>();
            kitchen.sizzle.loop = true;
            kitchen.sizzle.playOnAwake = false;
            kitchen.sizzle.spatialBlend = 1;
            kitchen.sizzle.minDistance = 1.15f;
            kitchen.sizzle.maxDistance = 9f;

            kitchen.pot = Node(root, "Open stockpot", new Vector3(1.25f, .985f, -.04f));
            Vessel(kitchen.pot, .38f, .31f, steel, tin);
            AddAction(kitchen.pot, kitchen, KitchenAction.Pot);
            kitchen.broth = Shape(kitchen.pot, PrimitiveType.Cylinder, "Rising broth", new Vector3(0, .11f, 0),
                new Vector3(.64f, .01f, .64f), soup, false);
            kitchen.broth.gameObject.SetActive(false);
            kitchen.lid = Shape(kitchen.pot, PrimitiveType.Cylinder, "Rattling lid", new Vector3(.25f, .255f, .38f),
                new Vector3(.61f, .026f, .61f), tin, false);
            kitchen.lid.localRotation = Quaternion.Euler(63f, 0, 0);
            foreach (float side in new[] { -1f, 1f })
                Box(kitchen.pot, "Stockpot handle", new Vector3(side * .465f, .25f, 0), new Vector3(.16f, .044f, .18f), trim, false);
            var ladle = Button(root, kitchen, KitchenAction.Serve, "Soup ladle rest", new Vector3(2.09f, .996f, -.13f),
                new Vector3(.40f, .083f, .39f), enamel);
            Shape(ladle, PrimitiveType.Sphere, "Ladle bowl", new Vector3(.07f, .077f, .023f), new Vector3(.15f, .068f, .151f), tin, false);
            Box(ladle, "Wood ladle grip", new Vector3(-.108f, .085f, -.051f), new Vector3(.26f, .037f, .039f), trim, false)
                .localRotation = Quaternion.Euler(0, -22, 0);
            var bin = Node(root, "Scrap bin", new Vector3(2.92f, -.07f, -.18f));
            Vessel(bin, .285f, .52f, enamel, steel);
            AddAction(bin, kitchen, KitchenAction.Bin);

            var plate = game.lunch.physicsTray.contents[0].transform;
            var meal = Node(plate, "Kitchen soup portion", new Vector3(0, .064f, 0));
            meal.localScale = Vector3.one;
            Shape(meal, PrimitiveType.Cylinder, "Soup bowl", Vector3.zero, new Vector3(.252f, .033f, .252f), tin, false);
            Shape(meal, PrimitiveType.Cylinder, "Soup surface", new Vector3(0, .031f, 0),
                new Vector3(.212f, .004f, .212f), soup, false);
            for (int i = 0; i < 3; i++)
                Box(meal, "Stewed potato", new Vector3(-.048f + i * .044f, .038f, -.032f + i * .022f),
                    new Vector3(.034f, .017f, .030f), kitchen.potatoPaper, false).localRotation = Quaternion.Euler(0, i * 31, 0);
            kitchen.trayDust = Shape(meal, PrimitiveType.Sphere, "That piece from the floor", new Vector3(.047f, .039f, -.034f),
                new Vector3(.053f, .024f, .038f), dust, false).gameObject;
            kitchen.traySoup = meal.gameObject;
            meal.gameObject.SetActive(false);

            var light = Node(root, "Kitchen work light", new Vector3(0, 3.1f, -.62f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 4.7f; light.intensity = 3.1f; light.color = Hex("FFE2BB");
            SpaceWorkAreas(kitchen);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        static void SpaceWorkAreas(FactoryKitchen kitchen)
        {
            Transform root = kitchen.transform;
            foreach (Transform child in root)
            {
                switch (child.name)
                {
                    case "Worktop support":
                    case "Laminated worktop":
                    case "Lower folded shelf":
                    case "Apron":
                    case "Back rail":
                        child.localScale = Vector3.Scale(child.localScale, new Vector3(1.42f, 1, 1));
                        goto case "Folded cabinet post";
                    case "Folded cabinet post":
                    case "Copper bracket":
                    case "Dark floor shoe":
                    case "Shelf brace":
                    case "Recessed drawer front":
                    case "Drawer pull":
                    case "Apron rivet":
                    case "Stack of spare bowls":
                        Vector3 original = child.localPosition;
                        child.localPosition = new Vector3(original.x * 1.42f + .48f, original.y, original.z);
                        break;
                    case "Rat stock":
                        child.localPosition += Vector3.left * .48f;
                        break;
                    case "Potato box":
                    case "Potato crate stand":
                        child.localPosition += Vector3.left * .72f;
                        break;
                    case "Board stand":
                    case "Carryable cutting board":
                    case "Prep dock":
                    case "Kitchen knife":
                    case "Knife rack":
                        child.localPosition += Vector3.left * .46f;
                        break;
                    case "Stove":
                    case "Pan rest":
                    case "Carryable frying pan":
                    case "Coals":
                    case "Burner switch":
                    case "Spatula":
                    case "Spatula handle":
                    case "Spatula head":
                    case "Smoke from scorched pieces":
                        child.localPosition += Vector3.right * .92f;
                        break;
                    case "Open stockpot":
                    case "Soup ladle rest":
                        child.localPosition += Vector3.right * 1.66f;
                        break;
                    case "Scrap bin":
                        child.localPosition = new Vector3(3.5f, -.07f, .94f);
                        break;
                    case "Kitchen work light":
                        child.localPosition += Vector3.right * .56f;
                        child.GetComponent<Light>().range = 6.2f;
                        break;
                }
            }
        }

        static void BuildKnife(FactoryKitchen kitchen, Material tin, Material steel, Material trim)
        {
            float length = FactoryKitchen.BladeHalfLength;
            var profile = new[] { new Vector2(-.036f, -length), new Vector2(.038f, -length),
                new Vector2(.033f, length - .088f), new Vector2(-.022f, length), new Vector2(-.036f, length - .10f) };
            var blade = Node(kitchen.knife, "Long tapered steel blade", new Vector3(0, .045f, 0));
            blade.localRotation = Quaternion.Euler(0, 0, 90);
            blade.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh("KitchenLongKnifeBlade", FactoryKitchenSlice.CreateMesh(profile, .017f));
            blade.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { tin, steel };
            Box(kitchen.knife, "Knife bolster", new Vector3(0, .059f, -length), new Vector3(.051f, .053f, .043f), steel, false);
            Box(kitchen.knife, "Knife handle", new Vector3(0, .059f, -length - .093f), new Vector3(.052f, .052f, .178f), trim, false);
            foreach (float offset in new[] { .048f, .135f })
                Shape(kitchen.knife, PrimitiveType.Cylinder, "Handle pin", new Vector3(0, .086f, -length - offset),
                    new Vector3(.014f, .002f, .014f), tin, false);
        }

        static Material PaintGuide()
        {
            const string path = "Assets/Game_Assets/Generated/KitchenBladeGuide.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Cardboard/Kitchen Blade Guide");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.renderQueue = -1;
            material.SetColor("_BaseColor", new Color(1, .84f, .46f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh BakeRat(GameObject root, Material cutEdge, out Material[] materials)
        {
            var palette = AssetDatabase.LoadAllAssetsAtPath("Assets/Game_Assets/Cardboard/Sewer/RatHunt/Rat Hunt Props.asset").OfType<Material>().ToArray();
            var fur = palette.First(material => material.name == "Rat fur");
            var tail = palette.First(material => material.name == "Rat tail");
            var parts = new List<CombineInstance>();
            var surfaces = new List<Material>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<Renderer>();
                var resolved = new Material[mesh.subMeshCount];
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    resolved[i] = mesh.subMeshCount == 2 && i == 0 ? tail : fur;
                    surfaces.Add(resolved[i]);
                }
                renderer.sharedMaterials = resolved;
            }
            var model = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            model.CombineMeshes(parts.ToArray(), false, true);
            var bounds = model.bounds;
            Quaternion orient = bounds.size.z > bounds.size.x ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            float scale = .557f / Mathf.Max(bounds.size.x, bounds.size.z);
            Vector3 resize = new Vector3(scale, Mathf.Min(scale, .116f / Mathf.Max(bounds.size.y, .001f)),
                Mathf.Min(scale, .291f / Mathf.Max(Mathf.Min(bounds.size.x, bounds.size.z), .001f)));
            var vertices = model.vertices;
            var normals = model.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(orient * (vertices[i] - bounds.center), resize);
                normals[i] = Vector3.Scale(orient * normals[i], new Vector3(1 / resize.x, 1 / resize.y, 1 / resize.z)).normalized;
            }
            model.vertices = vertices;
            model.normals = normals;
            model.RecalculateBounds();
            model.subMeshCount++;
            model.SetTriangles(Array.Empty<int>(), model.subMeshCount - 1);
            surfaces.Add(cutEdge);
            materials = surfaces.ToArray();
            return SaveMesh("KitchenRatSource", model);
        }

        static Mesh BuildPotatoMesh()
        {
            const int rings = 11, sides = 24, strips = 6;
            var vertices = new List<Vector3> { new Vector3(-.223f, 0, .006f) };
            var uv = new List<Vector2> { new Vector2(0, .5f) };
            var triangles = new List<int>[strips];
            for (int i = 0; i < strips; i++) triangles[i] = new List<int>();
            for (int ring = 1; ring <= rings; ring++)
            {
                float latitude = Mathf.PI * ring / (rings + 1);
                float radius = Mathf.Pow(Mathf.Sin(latitude), .82f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * (Mathf.PI * 2 / sides);
                    float lump = 1 + .09f * Mathf.Sin(angle * 3 + latitude * 4) + .028f * Mathf.Cos(angle * 7 - latitude * 5);
                    vertices.Add(new Vector3(-.223f * Mathf.Cos(latitude), .081f * radius * lump * Mathf.Sin(angle),
                        .116f * radius * lump * Mathf.Cos(angle) + .018f * Mathf.Sin(latitude * 2)));
                    uv.Add(new Vector2(ring / (rings + 1f), side / (float)sides));
                }
            }
            int end = vertices.Count;
            vertices.Add(new Vector3(.223f, -.005f, 0));
            uv.Add(new Vector2(1, .5f));
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                var indices = triangles[side / (sides / strips)];
                indices.AddRange(new[] { 0, 1 + side, 1 + next });
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int a = 1 + ring * sides + side, b = 1 + ring * sides + next;
                    indices.AddRange(new[] { a, a + sides, b + sides, a, b + sides, b });
                }
                indices.AddRange(new[] { 1 + (rings - 1) * sides + side, end, 1 + (rings - 1) * sides + next });
            }
            var mesh = new Mesh { subMeshCount = strips + 1 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            for (int i = 0; i < strips; i++) mesh.SetTriangles(triangles[i], i);
            mesh.SetTriangles(Array.Empty<int>(), strips);
            mesh.RecalculateNormals();
            return SaveMesh("KitchenPotatoSource", mesh);
        }

        static Mesh SaveMesh(string asset, Mesh generated)
        {
            generated.name = asset;
            string path = "Assets/Game_Assets/Generated/" + asset + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = generated; AssetDatabase.CreateAsset(mesh, path); }
            else { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            return mesh;
        }

        static void BuildBench(Transform root, Material wood, Material grain, Material trim, Material enamel, Material tin)
        {
            var collision = Box(root, "Worktop support", new Vector3(0, .90f, 0), new Vector3(4.96f, .10f, 1.04f), wood);
            collision.GetComponent<Renderer>().enabled = false;
            Sheet(root, "Laminated worktop", "KitchenLaminatedTop", new Vector3(0, .90f, 0), 5f, 1.08f, .10f, .085f, wood, trim);
            Sheet(root, "Lower folded shelf", "KitchenLowerShelf", new Vector3(0, .19f, .027f), 4.68f, .89f, .064f, .07f, grain, trim);
            foreach (float x in new[] { -2.34f, -.74f, .74f, 2.34f })
            {
                foreach (float z in new[] { -.415f, .415f })
                {
                    Box(root, "Folded cabinet post", new Vector3(x, .36f, z), new Vector3(.122f, 1.02f, .112f), grain);
                    Box(root, "Copper bracket", new Vector3(x, .77f, z), new Vector3(.16f, .20f, .135f), enamel, false);
                    Box(root, "Dark floor shoe", new Vector3(x, -.098f, z), new Vector3(.16f, .089f, .148f), trim, false);
                }
                Box(root, "Shelf brace", new Vector3(x, .132f, 0), new Vector3(.125f, .065f, .94f), trim, false);
            }
            Box(root, "Apron", new Vector3(0, .766f, -.48f), new Vector3(4.81f, .174f, .086f), trim);
            Box(root, "Back rail", new Vector3(0, .985f, .485f), new Vector3(4.81f, .070f, .054f), grain);
            foreach (float x in new[] { -1.95f, 1.27f, 1.98f })
            {
                Box(root, "Recessed drawer front", new Vector3(x, .67f, -.462f), new Vector3(.58f, .20f, .055f), enamel);
                Box(root, "Drawer pull", new Vector3(x, .67f, -.516f), new Vector3(.19f, .035f, .061f), tin, false);
            }
            foreach (float x in new[] { -2.32f, -1.48f, -.62f, .62f, 1.48f, 2.32f })
                Shape(root, PrimitiveType.Cylinder, "Apron rivet", new Vector3(x, .807f, -.529f),
                    new Vector3(.022f, .008f, .022f), tin, false).localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 3; i++)
                Shape(root, PrimitiveType.Cylinder, "Stack of spare bowls", new Vector3(2.01f, .257f + i * .052f, .07f),
                    new Vector3(.36f, .026f, .36f), i % 2 == 0 ? tin : enamel, false);
        }

        static void CrateRim(Transform node, float width, float depth, Material face, Material trim)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Box(node, "Folded side", new Vector3(side * width * .47f, .1f, 0), new Vector3(.04f, .18f, depth), face);
                Box(node, "Crate end lip", new Vector3(0, .104f, side * depth * .47f), new Vector3(width, .18f, .036f), face);
                Box(node, "Reinforced corner", new Vector3(side * width * .47f, .11f, -depth * .486f), new Vector3(.082f, .19f, .028f), trim, false);
            }
        }

        static void FitProp(GameObject model, Vector3 foot, float size)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localScale *= size / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            model.transform.position += foot - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        static void Sheet(Transform parent, string name, string asset, Vector3 position, float width, float depth,
            float thickness, float corner, Material face, Material edge)
        {
            var outline = Rect(width, depth, corner);
            var generated = FactoryKitchenSlice.CreateMesh(outline, thickness);
            var uv = generated.uv;
            for (int i = 0; i < outline.Length * 2; i++)
                uv[i] = new Vector2(.043f + (.5f + uv[i].y / (depth * 5)) * .353f,
                    .075f + (.5f + uv[i].x / (width * 5)) * .824f);
            generated.uv = uv;
            var mesh = SaveMesh(asset, generated);
            var node = Node(parent, name, position);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { face, edge };
        }

        static ParticleSystem BuildSmoke(Transform root, Vector3 position)
        {
            var node = Node(root, "Smoke from scorched pieces", position);
            node.localRotation = Quaternion.Euler(-90, 0, 0);
            var system = node.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .58f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .23f);
            main.startColor = new Color(.29f, .255f, .23f, .7f);
            main.maxParticles = 28;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16;
            shape.radius = .14f;
            var emission = system.emission;
            emission.rateOverTime = 13;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.8f, .20f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Game_Assets/Generated/AirborneBeigeDust.mat");
            return system;
        }

        static FactoryKitchenTool Tool(Transform root, FactoryKitchen kitchen, Transform home, bool pan)
        {
            var node = Node(root, pan ? "Carryable frying pan" : "Carryable cutting board", home.localPosition);
            AddAction(node, kitchen, pan ? KitchenAction.Pan : KitchenAction.Board);
            var body = node.gameObject.AddComponent<Rigidbody>();
            body.mass = pan ? 1.9f : 1.1f;
            body.useGravity = false;
            body.isKinematic = true;
            node.gameObject.AddComponent<FactoryPhysicsProp>().itemName = pan ? "Frying pan" : "Chopping board";
            var tool = node.gameObject.AddComponent<FactoryKitchenTool>();
            tool.home = home;
            tool.pan = pan;
            return tool;
        }

        static void Vessel(Transform node, float radius, float height, Material shell, Material baseMaterial)
        {
            Shape(node, PrimitiveType.Cylinder, "Round base", new Vector3(0, -.007f, 0), new Vector3(radius * 2, .025f, radius * 2), baseMaterial, false);
            foreach (float angle in new[] { 0f, 45f })
            {
                var plate = node.gameObject.AddComponent<BoxCollider>();
                if (angle == 0f) plate.size = new Vector3(radius * 2f, .045f, radius * 1.42f);
                else plate.size = new Vector3(radius * 1.42f, .045f, radius * 2f);
            }
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var wall = Box(node, "Folded rim", direction * radius + Vector3.up * (height / 2),
                    new Vector3(radius * .55f, height, .03f), shell);
                wall.localRotation = Quaternion.Euler(0, angle, 0);
            }
        }

        static Transform Button(Transform parent, FactoryKitchen kitchen, KitchenAction action, string name,
            Vector3 pos, Vector3 size, Material material)
        {
            var node = Node(parent, name, pos);
            Box(node, name + " plate", Vector3.zero, size, material);
            AddAction(node, kitchen, action);
            return node;
        }

        static void GripVolume(Transform node, Vector3 center, Vector3 size)
        {
            var collider = node.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            collider.isTrigger = true;
        }

        static void AddAction(Transform node, FactoryKitchen kitchen, KitchenAction action)
        {
            var control = node.gameObject.AddComponent<FactoryKitchenControl>();
            control.kitchen = kitchen;
            control.action = action;
            node.gameObject.AddComponent<FactoryInteractable>().action = FactoryAction.Kitchen;
        }

        static Transform Node(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = true) =>
            Shape(parent, PrimitiveType.Cube, name, position, scale, material, solid);

        static Transform Shape(Transform parent, PrimitiveType primitive, string name, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var node = GameObject.CreatePrimitive(primitive);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.transform.localScale = scale;
            node.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(node.GetComponent<Collider>());
            return node.transform;
        }

        static Material Paint(string name, string color, string texture = null) => FactoryPresentationInstaller.Paper(
            "Kitchen" + name, Hex(color), texture == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TableTextures + texture));
    }
}
