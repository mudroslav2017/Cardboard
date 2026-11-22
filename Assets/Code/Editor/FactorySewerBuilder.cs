using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactorySewerBuilder
    {
        const string AssetFolder = "Assets/Game_Assets/Cardboard/Sewer/";
        const string ScenePath = "Assets/Scenes/FactoryShift.unity";
        internal const string RootName = "Sewer • maintenance tunnels";
        const float ModuleScale = 4.5f;
        const float Pitch = 3.45f * ModuleScale;
        const float BlockWidth = Pitch / 10;
        const float BlockHeight = .55f * ModuleScale;
        const float BaseY = -5.25f * ModuleScale;
        const uint LightLayer = 1u << 3;
        const int North = 1, East = 2, South = 4, West = 8;

        // Each opening connects to its neighbor. A and B replace the end cells with the longer entry model.
        static readonly string[] Layout =
        {
            "A┬┬┐┌─┬┐",
            " ├┴┤├─┤│",
            "┌┤┌┘│┌┤│",
            "│├┴┬┴┤├┘",
            "└┴─┴─┴┴B"
        };

        static readonly string[] WaterRoute =
        {
            "++++    ",
            "   ++++ ",
            "  +++++ ",
            "  ++++  ",
            "     +++"
        };

        static readonly (string name, int openings)[] Corners =
        {
            ("rotate1", North | East), ("rotate2", East | South), ("rotate3", North | East),
            ("rotate4", North | West), ("rotate5", South | West)
        };

        static readonly (int opening, Vector3 direction)[] Directions =
        {
            (North, Vector3.forward), (East, Vector3.right), (South, Vector3.back), (West, Vector3.left)
        };

        [MenuItem("Cardboard/Build factory sewer")]
        public static void Build()
        {
            var world = Object.FindAnyObjectByType<CardboardFactoryWorld>();
            if (!world || world.gameObject.scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open FactoryShift in Edit Mode.");

            var old = world.transform.Find(RootName);
            if (old) Object.DestroyImmediate(old.gameObject);
            var builder = new Builder(world.transform);
            builder.Build();
            FactoryRatHuntBuilder.Install(world.transform.Find(RootName));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        }

        public static void BuildBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Build();
            EditorSceneManager.SaveScene(scene);
        }

        static int Openings(char tile) => tile switch
        {
            'A' => East, 'B' => West,
            '└' => North | East, '│' => North | South, '┘' => North | West,
            '┌' => East | South, '─' => East | West, '┐' => South | West,
            '├' => North | East | South, '┴' => North | East | West,
            '┬' => East | South | West, '┤' => North | South | West,
            _ => 0
        };

        internal static IEnumerable<(string from, string to)> TunnelConnections()
        {
            for (int row = 0; row < Layout.Length; row++)
            for (int column = 0; column < Layout[row].Length; column++)
            {
                char tile = Layout[row][column];
                if (tile == 'A' || tile == 'B') continue;
                int openings = Openings(tile);
                if ((openings & East) != 0 && Layout[row][column + 1] != 'B')
                    yield return (TileName(row, column), TileName(row, column + 1));
                if ((openings & South) != 0)
                    yield return (TileName(row, column), TileName(row + 1, column));
            }
        }

        static string TileName(int row, int column) => $"{row + 1:D2}.{column + 1:D2}";

        static Vector3 Cell(int column, int row)
        {
            float x = CardboardFactoryWorld.WorkshopCenterX + (column - (Layout[0].Length - 1) * .5f) * Pitch;
            float z = -1.45f + ((Layout.Length - 1) * .5f - row) * Pitch;
            return new Vector3(x, BaseY, z);
        }

        static Quaternion RotationTo(int source, int target)
        {
            for (int turns = 0; turns < 4; turns++)
            {
                if (source == target) return Quaternion.Euler(0, turns * 90, 0);
                source = ((source << 1) | (source >> 3)) & 15;
            }
            throw new InvalidOperationException($"Cannot rotate openings {source} to {target}.");
        }

        sealed class Builder
        {
            readonly Transform world, root, tiles, fixtures, accesses;
            readonly Material stone, block, waterBlue, waterSludge, metal, stripe, warmGlass, blueGlass;
            readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
            readonly Dictionary<Mesh, Mesh> waterMeshes = new Dictionary<Mesh, Mesh>();
            readonly Mesh straight;
            int cornerIndex;

            public Builder(Transform world)
            {
                this.world = world;
                root = Group(world, RootName, Vector3.zero);
                tiles = Group(root, "Tunnel tiles", Vector3.zero);
                fixtures = Group(root, "Lamps and route markers", Vector3.zero);
                accesses = Group(root, "Maintenance hatches", Vector3.zero);
                stone = Lit("Sewer Stone", new Color(.34f, .36f, .34f), .08f, new Color(.043f, .048f, .045f));
                block = Lit("Sewer Blocks", new Color(.245f, .28f, .275f), .13f, new Color(.03f, .033f, .032f));
                waterBlue = Lit("Sewer Blue Water", new Color(.085f, .28f, .37f), .79f, new Color(.028f, .09f, .13f));
                waterSludge = Lit("Sewer Dark Water", new Color(.13f, .22f, .21f), .72f, new Color(.016f, .034f, .031f));
                metal = Lit("Sewer Iron", new Color(.105f, .13f, .13f), .35f);
                stripe = Lit("Sewer Yellow Paint", new Color(.74f, .52f, .14f), .22f);
                warmGlass = Lit("Sewer Lamp Glass", new Color(.9f, .65f, .34f), .30f, new Color(1.5f, .85f, .38f));
                blueGlass = Lit("Sewer Blue Marker", new Color(.18f, .65f, .9f), .25f, new Color(.10f, .58f, 1.4f));
                foreach (var name in Corners.Select(corner => corner.name).Concat(new[] { "entry", "yrotate" }))
                {
                    var path = AssetFolder + name + ".fbx";
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                    models.Add(name, AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
                straight = BuildStraight(models["entry"].GetComponentInChildren<MeshFilter>().sharedMesh);
            }

            public void Build()
            {
                for (int row = 0; row < Layout.Length; row++)
                for (int column = 0; column < Layout[row].Length; column++)
                {
                    char tile = Layout[row][column];
                    int openings = Openings(tile);
                    if (openings == 0) continue;
                    bool blue = WaterRoute[row][column] == '+';
                    var center = Cell(column, row);
                    string label = TileName(row, column);
                    if (tile == 'A' || tile == 'B')
                    {
                        var inward = tile == 'A' ? Vector3.right : Vector3.left;
                        BuildEntry(label, tile, center + inward * (Pitch / 2), inward);
                        continue;
                    }

                    var holder = Group(tiles, label, center);
                    holder.localScale = new Vector3(BlockWidth, BlockHeight, BlockWidth);
                    if (openings == (North | South) || openings == (East | West))
                    {
                        holder.name += " • straight";
                        holder.localRotation = RotationTo(North | South, openings);
                        var tunnel = AddMesh(holder, "Straight tunnel", new Vector3(-5, 0, -5), straight, stone, block);
                        AddWater(tunnel, "straight", blue);
                    }
                    else if (Directions.Count(port => (port.opening & openings) != 0) == 3)
                    {
                        holder.name += " • junction";
                        holder.localRotation = RotationTo(East | South | West, openings);
                        AddModel(holder, "yrotate", new Vector3(-5, 0, -5), blue);
                    }
                    else
                    {
                        var corner = Corners[cornerIndex++ % Corners.Length];
                        holder.name += " • bend";
                        holder.localRotation = RotationTo(corner.openings, openings);
                        AddModel(holder, corner.name, new Vector3(-5, 0, -5), blue);
                    }

                    bool marker = (column == 2 && row == 0) || (column == 2 && row == 3) ||
                        (column == 4 && row == 1) || (column == 6 && row == 4);
                    if (marker || (column + row) % 2 == 0)
                        Lamp(center + Vector3.up * (3.59f * ModuleScale), marker);
                }
            }

            void BuildEntry(string label, char tile, Vector3 mouth, Vector3 inward)
            {
                var entry = Group(tiles, label + (tile == 'A' ? " • west stairwell" : " • east stairwell"), mouth);
                // The FBX opens at Z = 0, with its opening centered at X = 6.
                entry.localRotation = Quaternion.LookRotation(-inward);
                entry.localScale = new Vector3(BlockWidth, BlockHeight, .28f * ModuleScale);
                AddModel(entry, "entry", new Vector3(-6, 0, 0), true);
                Vector3 lowerPosition = entry.TransformPoint(new Vector3(-1.75f, 3, 7.15f)) + Vector3.up * .18f;
                var lower = Group(accesses, tile + " • sewer landing", lowerPosition);
                lower.localRotation = Quaternion.LookRotation(inward);

                float floor = FactoryEnvironmentInstaller.FloorY(world);
                // Keep the hatch lids in the workshop when the underground footprint changes.
                var surfacePosition = tile == 'A' ? new Vector3(-18.84f, floor, 1.59f) : new Vector3(8.24f, floor, -8.09f);
                var hatch = Group(accesses, tile + " • workshop hatch", surfacePosition);
                hatch.localRotation = Quaternion.LookRotation(inward);
                Box(hatch, "Hatch frame", new Vector3(0, .017f, 0), new Vector3(1.14f, .034f, 1.05f), metal);
                Box(hatch, "Steel hatch", new Vector3(0, .04f, 0), new Vector3(.98f, .025f, .90f), metal);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(hatch, "Painted edge", new Vector3(side * .48f, .056f, 0), new Vector3(.055f, .008f, .88f), stripe);
                    Box(hatch, "Recessed handle", new Vector3(side * .105f, .077f, 0), new Vector3(.05f, .05f, .11f), metal);
                }
                Box(hatch, "Handle grip", new Vector3(0, .102f, 0), new Vector3(.25f, .04f, .064f), metal);
                var surface = Group(hatch, "Workshop landing", new Vector3(0, 0, .98f));
                surface.localRotation = Quaternion.identity;
                Access(hatch, "E - DESCEND TO SEWER", new Vector3(0, .06f, 0), new Vector3(1.14f, .27f, 1.05f))
                    .Configure(lower, surface);

                var signFoot = entry.TransformPoint(new Vector3(-2.65f, 3, 8.8f));
                var climb = Group(accesses, tile + " • climb to workshop", signFoot + Vector3.up * 1.48f);
                climb.localRotation = entry.rotation;
                Box(climb, "Sign footing", Vector3.down * 1.43f, new Vector3(.27f, .10f, .22f), metal, LightLayer);
                Box(climb, "Sign post", Vector3.down * .79f, new Vector3(.07f, 1.34f, .07f), metal, LightLayer);
                Box(climb, "Access sign frame", Vector3.zero, new Vector3(.37f, .29f, .085f), metal, LightLayer);
                Box(climb, "Access sign", new Vector3(0, 0, -.052f), new Vector3(.26f, .20f, .026f), blueGlass, LightLayer);
                Access(climb, "E - CLIMB TO WORKSHOP", Vector3.zero, new Vector3(.85f, 1.0f, .25f))
                    .Configure(surface);
                Lamp(entry.TransformPoint(new Vector3(-.6f, 6.52f, 6.4f)), true);
            }

            static FactorySewerAccess Access(Transform root, string prompt, Vector3 center, Vector3 size)
            {
                var hit = root.gameObject.AddComponent<BoxCollider>();
                hit.center = center;
                hit.size = size;
                hit.isTrigger = true;
                var target = root.gameObject.AddComponent<FactoryInteractable>();
                target.action = FactoryAction.SewerAccess;
                target.prompt = prompt;
                return root.gameObject.AddComponent<FactorySewerAccess>();
            }

            void AddModel(Transform holder, string name, Vector3 position, bool blue)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(models[name], holder);
                model.transform.localPosition = position;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    ConfigureRenderer(renderer, LightLayer, stone, block);
                    filter.gameObject.layer = 8;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    AddWater(filter.transform, name, blue);
                }
            }

            void AddWater(Transform tile, string name, bool blue)
            {
                var source = tile.GetComponent<MeshFilter>().sharedMesh;
                if (!waterMeshes.TryGetValue(source, out var water))
                {
                    var vertices = source.vertices;
                    var indices = source.triangles;
                    var surface = new List<Vector3>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        if (Mathf.Abs(a.y - 1) > .001f || Mathf.Abs(b.y - 1) > .001f || Mathf.Abs(c.y - 1) > .001f ||
                            Vector3.Cross(b - a, c - a).y <= 0)
                            continue;
                        if (name == "entry" && ((a.x + b.x + c.x) / 3 < 4.99f || (a.x + b.x + c.x) / 3 > 7.01f))
                            continue;
                        surface.Add(new Vector3(a.x, 2.92f, a.z));
                        surface.Add(new Vector3(b.x, 2.92f, b.z));
                        surface.Add(new Vector3(c.x, 2.92f, c.z));
                    }
                    water = new Mesh { name = "Water • " + source.name };
                    water.SetVertices(surface);
                    water.SetTriangles(Enumerable.Range(0, surface.Count).ToArray(), 0);
                    water.RecalculateNormals();
                    water = SaveMesh("Water " + name, water);
                    waterMeshes.Add(source, water);
                }
                AddMesh(tile, "Shallow channel", Vector3.zero, water, blue ? waterBlue : waterSludge);
            }

            void Lamp(Vector3 position, bool marker)
            {
                var holder = Group(fixtures, marker ? "Blue route beacon" : "Tunnel bulkhead lamp", position);
                holder.localScale = Vector3.one * ModuleScale;
                Box(holder, "Lamp housing", Vector3.zero, new Vector3(.40f, .12f, .40f), metal, LightLayer);
                var lens = Box(holder, "Lamp lens", Vector3.down * .075f, new Vector3(.23f, .055f, .23f),
                    marker ? blueGlass : warmGlass, LightLayer);
                if (marker) lens.transform.localRotation = Quaternion.Euler(0, 45, 0);
                var light = Group(holder, "Light", Vector3.down * .25f).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = marker ? new Color(.35f, .70f, 1f) : new Color(1f, .77f, .53f);
                light.intensity = (marker ? 3.1f : 2.6f) * ModuleScale * ModuleScale;
                light.range = 5.1f * ModuleScale;
                light.GetUniversalAdditionalLightData().renderingLayers = LightLayer;
            }

            static Mesh BuildStraight(Mesh entry)
            {
                // The entrance has an unbroken cross section between Z = 2 and Z = 3.
                var vertices = entry.vertices;
                var section = new Mesh { name = "Straight sewer section", subMeshCount = entry.subMeshCount };
                var points = new List<Vector3>();
                var surfaces = new List<int[]>();
                for (int material = 0; material < entry.subMeshCount; material++)
                {
                    var triangles = entry.GetTriangles(material);
                    var faceIndices = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var a = vertices[triangles[i]];
                        var b = vertices[triangles[i + 1]];
                        var c = vertices[triangles[i + 2]];
                        if (!WithinSection(a) || !WithinSection(b) || !WithinSection(c) ||
                            Mathf.Approximately(a.z, b.z) && Mathf.Approximately(a.z, c.z))
                            continue;
                        faceIndices.Add(points.Count);
                        faceIndices.Add(points.Count + 1);
                        faceIndices.Add(points.Count + 2);
                        points.Add(Stretch(a));
                        points.Add(Stretch(b));
                        points.Add(Stretch(c));
                    }
                    surfaces.Add(faceIndices.ToArray());
                }
                section.SetVertices(points);
                for (int material = 0; material < surfaces.Count; material++)
                    section.SetTriangles(surfaces[material], material);
                section.RecalculateNormals();
                return SaveMesh("Straight tunnel", section);
            }

            static bool WithinSection(Vector3 vertex) => vertex.z >= 1.999f && vertex.z <= 3.001f && vertex.x >= .999f;
            static Vector3 Stretch(Vector3 vertex) => new Vector3(vertex.x - 1, vertex.y, (vertex.z - 2) * 10);
        }

        static Transform Group(Transform parent, string name, Vector3 position)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        static Transform AddMesh(Transform parent, string name, Vector3 position, Mesh mesh, params Material[] materials)
        {
            var node = Group(parent, name, position);
            node.gameObject.layer = 8;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ConfigureRenderer(node.gameObject.AddComponent<MeshRenderer>(), LightLayer, materials);
            node.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return node;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, uint lightLayer = 1)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            ConfigureRenderer(box.GetComponent<MeshRenderer>(), lightLayer, material);
            return box;
        }

        static void ConfigureRenderer(MeshRenderer renderer, uint lightLayer, params Material[] materials)
        {
            renderer.sharedMaterials = materials;
            renderer.renderingLayerMask = lightLayer;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Lit(string name, Color color, float smoothness, Color emission = default)
        {
            string path = AssetFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = AssetFolder + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
