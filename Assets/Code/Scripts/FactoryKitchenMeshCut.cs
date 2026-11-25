using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    static class FactoryKitchenMeshCut
    {
        const float Epsilon = .00004f;
        const float WeldDistance = .0003f;

        struct Vertex
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public static Vertex Mix(Vertex a, Vertex b, float t) => new Vertex
            {
                position = Vector3.LerpUnclamped(a.position, b.position, t),
                normal = Vector3.LerpUnclamped(a.normal, b.normal, t).normalized,
                uv = Vector2.LerpUnclamped(a.uv, b.uv, t)
            };
        }

        public static bool TrySplit(Mesh mesh, Vector3 point, Vector3 normal, Vector3 blade, float halfLength,
            out Mesh first, out Mesh second)
        {
            first = second = null;
            var bounds = mesh.bounds;
            float distance = Vector3.Dot(bounds.center - point, normal);
            float reach = Mathf.Abs(normal.x) * bounds.extents.x + Mathf.Abs(normal.y) * bounds.extents.y + Mathf.Abs(normal.z) * bounds.extents.z;
            if (Mathf.Abs(distance) >= reach - Epsilon) return false;

            var positions = mesh.vertices;
            var normals = mesh.normals;
            var texcoords = mesh.uv;
            var vertices = new Vertex[positions.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vertex
            { position = positions[i], normal = normals[i], uv = texcoords.Length == positions.Length ? texcoords[i] : Vector2.zero };
            var positive = new Half(mesh.subMeshCount);
            var negative = new Half(mesh.subMeshCount);
            var seam = new List<(Vector3 a, Vector3 b)>();
            var triangle = new Vertex[3];
            var distances = new float[3];
            var polygon = new Vertex[4];
            for (int material = 0; material < mesh.subMeshCount; material++)
            {
                var indices = mesh.GetTriangles(material);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        triangle[corner] = vertices[indices[i + corner]];
                        float signed = Vector3.Dot(triangle[corner].position - point, normal);
                        distances[corner] = Mathf.Abs(signed) < Epsilon ? 0 : signed;
                    }
                    var a = new Vector3();
                    var b = new Vector3();
                    int crossings = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        int next = (j + 1) % 3;
                        if (distances[j] * distances[next] > 0 || distances[j] == 0 && distances[next] == 0) continue;
                        var hit = Vertex.Mix(triangle[j], triangle[next], distances[j] / (distances[j] - distances[next])).position;
                        if (crossings == 0) { a = hit; crossings = 1; }
                        else if ((hit - a).sqrMagnitude > Epsilon * Epsilon) { b = hit; crossings = 2; }
                    }
                    if (crossings == 2)
                    {
                        if (Mathf.Abs(Vector3.Dot(a - point, blade)) > halfLength || Mathf.Abs(Vector3.Dot(b - point, blade)) > halfLength)
                            return false;
                        seam.Add((a, b));
                    }
                    Clip(positive, material, triangle, distances, polygon, 1);
                    Clip(negative, material, triangle, distances, polygon, -1);
                }
            }
            if (seam.Count == 0 || !positive.HasSurface || !negative.HasSurface) return false;
            Cap(positive, negative, seam, normal);
            first = positive.Build();
            second = negative.Build();
            return true;
        }

        static void Clip(Half half, int material, Vertex[] triangle, float[] distances, Vertex[] polygon, int sign)
        {
            int count = 0;
            for (int i = 0; i < 3; i++)
            {
                int next = (i + 1) % 3;
                if (distances[i] * sign >= 0) polygon[count++] = triangle[i];
                if (distances[i] * distances[next] < 0)
                    polygon[count++] = Vertex.Mix(triangle[i], triangle[next], distances[i] / (distances[i] - distances[next]));
            }
            for (int i = 1; i + 1 < count; i++) half.Add(material, polygon[0], polygon[i], polygon[i + 1]);
        }

        static void Cap(Half positive, Half negative, List<(Vector3 a, Vector3 b)> seam, Vector3 normal)
        {
            var u = Vector3.Cross(Vector3.up, normal).normalized;
            var v = Vector3.Cross(normal, u);
            var loop = new List<Vector3>();
            while (seam.Count > 0)
            {
                int last = seam.Count - 1;
                var initial = seam[last];
                seam.RemoveAt(last);
                loop.Clear();
                loop.Add(initial.a);
                loop.Add(initial.b);
                while ((loop[loop.Count - 1] - loop[0]).sqrMagnitude > WeldDistance * WeldDistance)
                {
                    Vector3 end = loop[loop.Count - 1];
                    int found = seam.FindIndex(edge => (edge.a - end).sqrMagnitude <= WeldDistance * WeldDistance ||
                        (edge.b - end).sqrMagnitude <= WeldDistance * WeldDistance);
                    if (found < 0) break;
                    var edge = seam[found];
                    loop.Add((edge.a - end).sqrMagnitude <= WeldDistance * WeldDistance ? edge.b : edge.a);
                    seam.RemoveAt(found);
                }
                if (loop.Count < 4 || (loop[loop.Count - 1] - loop[0]).sqrMagnitude > WeldDistance * WeldDistance) continue;
                loop.RemoveAt(loop.Count - 1);
                FillLoop(positive, negative, loop, normal, u, v);
            }
        }

        static float Turn(Vector2 a, Vector2 b, Vector2 c) => (b.x-a.x)*(c.y-a.y) - (b.y-a.y)*(c.x-a.x);

        static void FillLoop(Half positive, Half negative, List<Vector3> loop, Vector3 normal, Vector3 u, Vector3 v)
        {
            var points = loop.ConvertAll(p => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)));
            float area = 0;
            for (int i = 0; i < points.Count; i++) area += Turn(Vector2.zero, points[i], points[(i + 1) % points.Count]);
            if (area < 0) { points.Reverse(); loop.Reverse(); }
            int cursor = 0, rejected = 0;
            while (points.Count >= 3 && rejected < points.Count)
            {
                int before = (cursor + points.Count - 1) % points.Count;
                int after = (cursor + 1) % points.Count;
                var a = points[before]; var b = points[cursor]; var c = points[after];
                float turn = Turn(a, b, c);
                bool ear = turn > 0;
                if (ear)
                    for (int i = 0; i < points.Count; i++)
                        if (i != before && i != cursor && i != after && Turn(a,b,points[i]) > 0 && Turn(b,c,points[i]) > 0 && Turn(c,a,points[i]) > 0)
                        { ear = false; break; }
                if (ear)
                {
                    negative.AddCap(loop[before], loop[cursor], loop[after], normal, u, v);
                    positive.AddCap(loop[before], loop[after], loop[cursor], -normal, u, v);
                }
                if (ear || Mathf.Abs(turn) < 1e-9f)
                {
                    points.RemoveAt(cursor);
                    loop.RemoveAt(cursor);
                    rejected = 0;
                }
                else { cursor++; rejected++; }
                if (cursor >= points.Count) cursor = 0;
            }
        }

        public static float Volume(Mesh mesh)
        {
            var vertices = mesh.vertices;
            float result = 0;
            for (int material = 0; material < mesh.subMeshCount; material++)
            {
                var indices = mesh.GetTriangles(material);
                for (int i = 0; i < indices.Length; i += 3)
                    result += Vector3.Dot(vertices[indices[i]], Vector3.Cross(vertices[indices[i+1]], vertices[indices[i+2]]));
            }
            return Mathf.Abs(result) / 6f;
        }

        public static Mesh CreateCollisionMesh(Mesh model)
        {
            var vertices = model.vertices;
            var outline = new List<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                float angle = i * (Mathf.PI * 2 / 24);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = float.NegativeInfinity;
                Vector2 support = default;
                foreach (var vertex in vertices)
                {
                    var point = new Vector2(vertex.x, vertex.z);
                    float along = Vector2.Dot(point, direction);
                    if (along <= distance) continue;
                    distance = along;
                    support = point;
                }
                if (!outline.Exists(point => (point - support).sqrMagnitude < 1e-9f)) outline.Add(support);
            }
            return outline.Count >= 3 && FactoryKitchenSlice.Area(outline) > .000002f
                ? FactoryKitchenSlice.CreateMesh(outline.ToArray(), Mathf.Max(.015f, model.bounds.size.y)) : null;
        }

        sealed class Half
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] groups;
            public bool HasSurface => positions.Count > 0;

            public Half(int materials)
            {
                groups = new List<int>[materials];
                for (int i = 0; i < materials; i++) groups[i] = new List<int>();
            }

            public void Add(int material, Vertex a, Vertex b, Vertex c)
            {
                if (Vector3.Cross(b.position - a.position, c.position - a.position).sqrMagnitude < 1e-16f) return;
                AddVertex(material,a); AddVertex(material,b); AddVertex(material,c);
            }

            void AddVertex(int material, Vertex vertex)
            {
                groups[material].Add(positions.Count);
                positions.Add(vertex.position); normals.Add(vertex.normal); uv.Add(vertex.uv);
            }

            public void AddCap(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Vector3 u, Vector3 v) =>
                Add(groups.Length - 1, CapVertex(a, normal, u, v), CapVertex(b, normal, u, v), CapVertex(c, normal, u, v));

            static Vertex CapVertex(Vector3 point, Vector3 normal, Vector3 u, Vector3 v) => new Vertex
            {
                position = point, normal = normal,
                uv = new Vector2(.92f + Mathf.Clamp(Vector3.Dot(point, v), -.12f, .12f) * .035f, .5f + Mathf.Clamp(Vector3.Dot(point, u), -.36f, .36f))
            };

            public Mesh Build()
            {
                var mesh = new Mesh { name = "Sliced ingredient", subMeshCount = groups.Length };
                if (positions.Count > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
                for (int i = 0; i < groups.Length; i++) mesh.SetTriangles(groups[i], i);
                return mesh;
            }
        }
    }
}
