using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    public static class FactoryKitchenSlice
    {
        public static float Area(IReadOnlyList<Vector2> polygon)
        {
            float sum = 0;
            for (int i = 0; i < polygon.Count; i++) sum += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            return Mathf.Abs(sum) * .5f;
        }

        public static Mesh CreateMesh(Vector2[] outline, float thickness)
        {
            int count = outline.Length;
            var vertices = new List<Vector3>(count * 6);
            var uv = new List<Vector2>(count * 6);
            var faces = new List<int>((count - 2) * 6);
            var edges = new List<int>(count * 6);
            for (int i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(outline[i].x, thickness / 2, outline[i].y));
                uv.Add(outline[i] * 5);
            }
            for (int i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(outline[i].x, -thickness / 2, outline[i].y));
                uv.Add(outline[i] * 5);
            }
            for (int i = 1; i < count - 1; i++)
            {
                faces.Add(0); faces.Add(i + 1); faces.Add(i);
                faces.Add(count); faces.Add(count + i); faces.Add(count + i + 1);
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int start = vertices.Count;
                vertices.Add(vertices[i]); vertices.Add(vertices[next]);
                vertices.Add(vertices[count + next]); vertices.Add(vertices[count + i]);
                float span = Mathf.Lerp(.12f, .88f, Mathf.Clamp01(Vector2.Distance(outline[i], outline[next]) / .5f));
                uv.Add(new Vector2(.917f, .12f)); uv.Add(new Vector2(.917f, span));
                uv.Add(new Vector2(.923f, span)); uv.Add(new Vector2(.923f, .12f));
                edges.Add(start); edges.Add(start + 1); edges.Add(start + 2);
                edges.Add(start); edges.Add(start + 2); edges.Add(start + 3);
            }
            var mesh = new Mesh { name = "Cut cardboard food", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(faces, 0);
            mesh.SetTriangles(edges, 1);
            mesh.RecalculateNormals();
            return mesh;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
