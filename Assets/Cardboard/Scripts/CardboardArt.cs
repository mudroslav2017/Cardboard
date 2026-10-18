using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    // All shapes are cut from thick, double-sided sheets. No imported art is required.
    public static class CardboardArt
    {
        public static readonly List<Object> Generated = new List<Object>();
        public static Material Kraft, Edge, Ink, Cream, Tape, Grass, Path, Coral, Teal, Gold, Leaf, DarkLeaf;
        static Material worldText;
        public static void Initialize()
        {
            Generated.Clear();
            worldText=null;
            CardboardCutout.Reset();
            Kraft = Paper("Natural kraft", "C69458"); Edge = Paper("Corrugated edges", "775035");
            Ink = Paper("Charcoal ink", "353D39"); Cream = Paper("Warm paper", "FFF0CF");
            Tape = Paper("Masking tape", "E2C588"); Grass = Paper("Sage island", "9AAB79");
            Path = Paper("Paper running track", "DCC69D"); Coral = Paper("Terracotta paint", "C96C51");
            Teal = Paper("Petrol paint", "4F9690"); Gold = Paper("Mustard paint", "DDB34F");
            Leaf = Paper("Sage foliage", "74936D"); DarkLeaf = Paper("Forest foliage", "436E5F");
        }

        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color c); return c; }

        static Material Paper(string name, string hex)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            var texture = new Texture2D(128, 128) { name = name + " fibers", wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(name.Length * 137);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float grain = (float)random.NextDouble() * .09f + .92f;
                if ((y % 17 == 0 && x % 9 < 6) || (x % 37 == 0 && y % 13 < 7)) grain -= .05f;
                pixels[y * 128 + x] = new Color(grain, grain, grain, 1);
            }
            texture.SetPixels(pixels); texture.Apply();
            mat.SetColor("_BaseColor", Hex(hex)); mat.color = Hex(hex);
            mat.SetTexture("_BaseMap", texture); mat.mainTexture = texture;
            mat.SetFloat("_Smoothness", 0); mat.SetFloat("_Metallic", 0);
            CardboardSurface.Configure(mat);
            Generated.Add(texture); Generated.Add(mat); return mat;
        }

        public static Transform Group(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        public static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = pos; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            return obj.transform;
        }

        public static Transform Panel(string name, Transform parent, Vector3 pos, Vector2[] outline, float thickness, Material face)
        {
            var t = Group(name, parent, pos);
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>();
            var front = new List<int>(); var sides = new List<int>(); int count = outline.Length;
            // Ear clipping also supports the concave paper crowns and fir trees.
            var faceTriangles = Triangulate(outline);
            for (int f = 0; f < 2; f++)
            {
                for (int i = 0; i < count; i++) { vertices.Add(new Vector3(outline[i].x, outline[i].y, (f == 0 ? 1 : -1) * thickness * .5f)); uvs.Add(outline[i] * .8f); }
                for (int i = 0; i < faceTriangles.Count; i += 3)
                { if (f == 0) front.AddRange(new[] {faceTriangles[i], faceTriangles[i+1], faceTriangles[i+2]}); else front.AddRange(new[] {count+faceTriangles[i], count+faceTriangles[i+2], count+faceTriangles[i+1]}); }
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count, start = vertices.Count;
                vertices.Add(new Vector3(outline[i].x, outline[i].y, thickness * .5f));
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -thickness * .5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, -thickness * .5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, thickness * .5f));
                uvs.AddRange(new[] {Vector2.zero, Vector2.up, Vector2.one, Vector2.right});
                sides.AddRange(new[] {start, start + 1, start + 2, start, start + 2, start + 3});
            }
            var mesh = new Mesh { name = name + " cut mesh", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(front, 0); mesh.SetTriangles(sides, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); Generated.Add(mesh);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] {face, Edge};
            return t;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;
        static List<int> Triangulate(Vector2[] outline)
        {
            var remaining = new List<int>(); var triangles = new List<int>();
            for (int i=0;i<outline.Length;i++) remaining.Add(i);
            int budget = outline.Length*outline.Length;
            while (remaining.Count > 2 && budget-- > 0)
            {
                bool clipped = false;
                for (int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count], b=remaining[i], c=remaining[(i+1)%remaining.Count];
                    if (Cross(outline[b]-outline[a],outline[c]-outline[b]) <= .000001f) continue;
                    bool occupied=false;
                    foreach (int p in remaining)
                    {
                        if (p==a || p==b || p==c) continue;
                        if (Cross(outline[b]-outline[a],outline[p]-outline[a])>=-.000001f &&
                            Cross(outline[c]-outline[b],outline[p]-outline[b])>=-.000001f &&
                            Cross(outline[a]-outline[c],outline[p]-outline[c])>=-.000001f) { occupied=true; break; }
                    }
                    if (occupied) continue;
                    triangles.AddRange(new[] {a,b,c}); remaining.RemoveAt(i); clipped=true; break;
                }
                if (!clipped) break;
            }
            return triangles;
        }

        public static Vector2[] Rect(float w, float h, float bevel = .04f)
        {
            float x = w / 2, y = h / 2, b = Mathf.Min(bevel, Mathf.Min(x, y) * .5f);
            return new[] {new Vector2(-x+b,-y), new Vector2(x-b,-y), new Vector2(x,-y+b), new Vector2(x,y-b), new Vector2(x-b,y), new Vector2(-x+b,y), new Vector2(-x,y-b), new Vector2(-x,-y+b)};
        }

        public static Transform Disc(string name, Transform parent, Vector3 pos, float radius, float depth, Material mat, int count = 32, float irregular = 0)
        {
            var outline = new Vector2[count];
            for (int i = 0; i < count; i++) { float a = i * Mathf.PI * 2 / count; float r = radius * (1 + Mathf.Sin(i * 7.13f) * irregular); outline[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
            return Panel(name, parent, pos, outline, depth, mat);
        }

        public static void Stroke(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            var t = Box(name, parent, (a + b) * .5f, new Vector3(width, (b - a).magnitude, .012f), material);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        public static Transform Label(string text, Transform parent, Vector3 position, float size, Material unused = null)
        {
            var t = Group(text, parent, position); t.localRotation = Quaternion.Euler(0, 180, 0);
            var mesh = t.gameObject.AddComponent<TextMesh>(); mesh.font=CardboardTypography.DefaultFont;
            mesh.text = text; mesh.fontSize = 32;
            mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.color = Ink.color;
            var shader=Shader.Find("Cardboard/WorldInk");
            if(shader)
            {
                if(!worldText) { worldText=new Material(shader) {name="Depth-tested world lettering"};Generated.Add(worldText); }
                mesh.font.RequestCharactersInTexture(text,mesh.fontSize,FontStyle.Normal);
                var label=t.gameObject.AddComponent<CardboardWorldLabel>();label.ink=worldText;label.Refresh();
            }
            return t;
        }
    }
}
