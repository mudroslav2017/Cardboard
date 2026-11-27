using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    sealed class FactoryPotatoSkin
    {
        const int Width = 192, Height = 128;
        const float BrushRadius = .054f, Feather = .010f;

        struct Sample
        {
            public Vector3 position, normal;
            public float area;
        }

        readonly Sample[] samples = new Sample[Width * Height];
        readonly Vector2[] brushPoints = new Vector2[Width * Height];
        readonly float[] facing = new float[Width * Height];
        readonly byte[] mask = new byte[Width * Height];
        readonly Color32[] colors = new Color32[Width * Height];
        readonly List<(int padding, int source)> padding = new List<(int, int)>();
        readonly Vector3[] vertices, projected;
        readonly int[] triangles;
        readonly Color peel, flesh;
        float angle = float.NaN;
        float surfaceArea, cleanedArea;

        public Mesh Mesh { get; }
        public Texture2D Texture { get; }
        public float Progress => Mathf.Clamp01(cleanedArea / surfaceArea);
        public float Bottom { get; private set; }

        public FactoryPotatoSkin(Mesh source, Color peel, Color flesh)
        {
            this.peel = peel;
            this.flesh = flesh;
            vertices = source.vertices;
            projected = new Vector3[vertices.Length];
            triangles = source.triangles;
            var renderVertices = new Vector3[triangles.Length];
            var renderNormals = new Vector3[triangles.Length];
            var renderUv = new Vector2[triangles.Length];
            var renderIndices = new int[triangles.Length];
            var normals = source.normals;
            var uv = source.uv;
            for (int face = 0; face < triangles.Length; face += 3)
            {
                for (int corner = face; corner < face + 3; corner++)
                {
                    int vertex = triangles[corner];
                    renderVertices[corner] = vertices[vertex];
                    renderNormals[corner] = normals[vertex];
                    renderUv[corner] = uv[vertex];
                    renderIndices[corner] = corner;
                }
                UnwrapFace(renderUv, face);
                RasterizeFace(face, renderVertices, renderNormals, renderUv);
            }
            Mesh = new Mesh { name = "Potato surface for continuous peeling", vertices = renderVertices, normals = renderNormals,
                uv = renderUv, triangles = renderIndices, bounds = source.bounds };
            Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Potato skin coverage", filterMode = FilterMode.Bilinear,
                wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat
            };
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = peel;
                surfaceArea += samples[i].area;
            }
            FindPadding();
            Upload();
        }

        // The potato mesh wraps at V=0 and shares a single vertex at each tip.
        static void UnwrapFace(Vector2[] uv, int face)
        {
            float low = 1f, high = 0f;
            int pole = -1;
            for (int i = face; i < face + 3; i++)
            {
                if (uv[i].x <= 0f || uv[i].x >= 1f) { pole = i; continue; }
                low = Mathf.Min(low, uv[i].y);
                high = Mathf.Max(high, uv[i].y);
            }
            if (high - low > .5f)
                for (int i = face; i < face + 3; i++)
                    if (i != pole && uv[i].y < .5f) uv[i].y += 1f;
            if (pole >= 0) uv[pole].y = (uv[face + (pole - face + 1) % 3].y + uv[face + (pole - face + 2) % 3].y) * .5f;
        }

        void RasterizeFace(int i, Vector3[] points, Vector3[] normals, Vector2[] uv)
        {
            Vector2 origin = uv[i], edge1 = uv[i + 1] - origin, edge2 = uv[i + 2] - origin;
            float determinant = Cross(edge1, edge2);
            if (Mathf.Abs(determinant) < .0000001f) return;
            Vector2 low = Vector2.Min(origin, Vector2.Min(uv[i + 1], uv[i + 2]));
            Vector2 high = Vector2.Max(origin, Vector2.Max(uv[i + 1], uv[i + 2]));
            Vector3 a = points[i], ab = points[i + 1] - a, ac = points[i + 2] - a;
            float area = Vector3.Cross(ab, ac).magnitude / Mathf.Abs(determinant);
            int left = Mathf.Max(0, Mathf.FloorToInt(low.x * Width));
            int right = Mathf.Min(Width - 1, Mathf.CeilToInt(high.x * Width));
            for (int y = Mathf.FloorToInt(low.y * Height); y <= Mathf.CeilToInt(high.y * Height); y++)
                for (int x = left; x <= right; x++)
                {
                    var offset = new Vector2((x + .5f) / Width, (y + .5f) / Height) - origin;
                    float u = Cross(offset, edge2) / determinant, v = Cross(edge1, offset) / determinant;
                    if (u < 0 || v < 0 || u + v > 1) continue;
                    samples[(y % Height) * Width + x] = new Sample
                    {
                        position = a + ab * u + ac * v,
                        normal = (normals[i] * (1f - u - v) + normals[i + 1] * u + normals[i + 2] * v).normalized,
                        area = area
                    };
                }
        }

        void FindPadding()
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].area > 0) continue;
                int x = i % Width, y = i / Width;
                for (int step = 1; step < Height / 2; step++)
                {
                    int before = ((y - step + Height) % Height) * Width + x;
                    int after = ((y + step) % Height) * Width + x;
                    int nearest = samples[before].area > 0 ? before : after;
                    if (samples[nearest].area == 0) continue;
                    padding.Add((i, nearest));
                    break;
                }
            }
        }

        public void Turn(float rotation)
        {
            if (angle == rotation) return;
            angle = rotation;
            Quaternion turn = Quaternion.AngleAxis(angle, Vector3.right);
            Bottom = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                projected[i] = turn * vertices[i];
                Bottom = Mathf.Min(Bottom, projected[i].y);
            }
            Vector3 up = Quaternion.Inverse(turn) * Vector3.up;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].area == 0) continue;
                brushPoints[i] = Flat(turn * samples[i].position);
                facing[i] = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.025f, .18f, Vector3.Dot(samples[i].normal, up)));
            }
        }

        public bool Scrape(Vector2 from, Vector2 to)
        {
            Vector2 stroke = to - from;
            float lengthSquared = stroke.sqrMagnitude;
            if (lengthSquared < .000001f) return false;
            bool changed = false;
            for (int i = 0; i < samples.Length; i++)
            {
                if (mask[i] == 255 || facing[i] == 0) continue;
                Vector2 delta = brushPoints[i] - from;
                float along = Mathf.Clamp01(Vector2.Dot(delta, stroke) / lengthSquared);
                float distanceSquared = (delta - stroke * along).sqrMagnitude;
                if (distanceSquared >= BrushRadius * BrushRadius) continue;
                float edge = Mathf.InverseLerp(BrushRadius, BrushRadius - Feather, Mathf.Sqrt(distanceSquared));
                byte coverage = (byte)Mathf.RoundToInt(255f * facing[i] * Mathf.SmoothStep(0, 1, edge));
                if (coverage <= mask[i]) continue;
                cleanedArea += samples[i].area * (coverage - mask[i]) / 255f;
                mask[i] = coverage;
                colors[i] = Color.Lerp(peel, flesh, coverage / 255f);
                changed = true;
            }
            if (changed) Upload();
            return changed;
        }

        void Upload()
        {
            foreach (var (pixel, source) in padding) colors[pixel] = colors[source];
            Texture.SetPixels32(colors);
            Texture.Apply(false);
        }

        public float HeightAt(Vector2 point, float minimum)
        {
            float height = minimum;
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

        public void Release()
        {
            Object.Destroy(Mesh);
            Object.Destroy(Texture);
        }

        static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
