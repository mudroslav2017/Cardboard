using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    sealed class FactoryPotatoSkin
    {
        readonly Mesh mesh;
        readonly Vector3[] vertices, projected;
        readonly int[] triangles;
        readonly bool[] peeled;
        readonly List<int> skinIndices = new List<int>();
        readonly List<int> cleanIndices = new List<int>();
        float angle = float.NaN;
        int totalPeeled;

        public Mesh Mesh => mesh;
        public float Progress => totalPeeled / (float)peeled.Length;

        public FactoryPotatoSkin(Mesh source)
        {
            vertices = source.vertices;
            projected = new Vector3[vertices.Length];
            var faces = new List<int>();
            for (int submesh = 0; submesh < source.subMeshCount; submesh++) faces.AddRange(source.GetTriangles(submesh));
            triangles = faces.ToArray();
            peeled = new bool[triangles.Length / 3];
            mesh = Object.Instantiate(source);
            mesh.name = "Potato with partially removed skin";
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triangles, 0, false);
            mesh.SetTriangles(System.Array.Empty<int>(), 1, false);
        }

        public void Turn(float rotation)
        {
            if (angle == rotation) return;
            angle = rotation;
            Quaternion turn = Quaternion.AngleAxis(angle, Vector3.right);
            for (int i = 0; i < vertices.Length; i++) projected[i] = turn * vertices[i];
        }

        public bool Scrape(Vector2 from, Vector2 to)
        {
            const float brushRadius = .027f;
            Vector2 stroke = to - from;
            float lengthSquared = stroke.sqrMagnitude;
            if (lengthSquared < .000001f) return false;
            int before = totalPeeled;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int face = i / 3;
                if (peeled[face]) continue;
                var a = projected[triangles[i]];
                var b = projected[triangles[i + 1]];
                var c = projected[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.y <= normal.magnitude * .08f) continue;
                Vector2 center = Flat((a + b + c) / 3);
                float along = Mathf.Clamp01(Vector2.Dot(center - from, stroke) / lengthSquared);
                if ((center - (from + stroke * along)).sqrMagnitude > brushRadius * brushRadius) continue;
                peeled[face] = true;
                totalPeeled++;
            }
            if (before == totalPeeled) return false;

            skinIndices.Clear();
            cleanIndices.Clear();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var surface = peeled[i / 3] ? cleanIndices : skinIndices;
                surface.Add(triangles[i]);
                surface.Add(triangles[i + 1]);
                surface.Add(triangles[i + 2]);
            }
            mesh.SetTriangles(skinIndices, 0, false);
            mesh.SetTriangles(cleanIndices, 1, false);
            return true;
        }

        public float HeightAt(Vector2 point)
        {
            float height = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = projected[triangles[i]], b = projected[triangles[i + 1]], c = projected[triangles[i + 2]];
                Vector2 ab = Flat(b - a), ac = Flat(c - a), ap = point - Flat(a);
                float size = Cross(ab, ac);
                if (Mathf.Abs(size) < .0000001f) continue;
                float u = Cross(ap, ac) / size, v = Cross(ab, ap) / size;
                if (u < 0 || v < 0 || u + v > 1) continue;
                height = Mathf.Max(height, a.y + (b.y - a.y) * u + (c.y - a.y) * v);
            }
            return height;
        }

        static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
