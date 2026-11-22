using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    // Split the mesh at each crease so the panels between them stay rigid and keep their printed UVs.
    internal sealed class FactoryCardboardBend
    {
        static readonly Vector2[] CreaseFractions =
        {
            new Vector2(.055f, .115f),
            new Vector2(.47f, .112f),
            new Vector2(.71f, .115f)
        };

        readonly struct Vertex
        {
            public readonly Vector3 Position, Normal;
            public readonly Vector2 UV;

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                UV = uv;
            }

            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex(
                Vector3.LerpUnclamped(a.Position, b.Position, t),
                Vector3.LerpUnclamped(a.Normal, b.Normal, t),
                Vector2.LerpUnclamped(a.UV, b.UV, t));
        }

        readonly struct CreasePose
        {
            public readonly Vector2 Start, End;
            public readonly float StartAngle, EndAngle, Curvature;

            public CreasePose(Vector2 start, float width, float incomingAngle, float fold)
            {
                Start = start;
                StartAngle = incomingAngle;
                EndAngle = incomingAngle + fold;
                Curvature = fold / width;
                End = start + Arc(incomingAngle, Curvature, width);
            }
        }

        readonly Vector3[] flatPositions, flatNormals, positions, normals;
        readonly Vector2[] creases = new Vector2[CreaseFractions.Length];
        readonly CreasePose[] pose = new CreasePose[CreaseFractions.Length];
        Vector3 previousAngles;
        public Mesh Mesh { get; }

        public FactoryCardboardBend(MeshFilter model)
        {
            var source = model.sharedMesh;
            var orientation = model.transform.localRotation;
            float size = source.bounds.size.y;
            float bottom = source.bounds.min.y;
            const int stripsPerCrease = 8;
            var planes = new float[creases.Length * (stripsPerCrease + 1)];
            for (int i = 0; i < creases.Length; i++)
            {
                float start = bottom + size * CreaseFractions[i].x;
                float width = size * CreaseFractions[i].y;
                creases[i] = new Vector2(start, width);
                for (int j = 0; j <= stripsPerCrease; j++)
                    planes[i * (stripsPerCrease + 1) + j] = start + width * j / stripsPerCrease;
            }

            var vertices = source.vertices;
            var sourceNormals = source.normals;
            var uv = source.uv;
            var input = new Vertex[vertices.Length];
            for (int i = 0; i < input.Length; i++)
                input[i] = new Vertex(orientation * vertices[i], orientation * sourceNormals[i], uv[i]);

            var output = new List<Vertex>();
            var submeshes = new List<int[]>();
            var polygon = new List<Vertex>(6);
            var clipped = new List<Vertex>(6);
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var indices = new List<int>();
                var triangles = source.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = input[triangles[i]];
                    var b = input[triangles[i + 1]];
                    var c = input[triangles[i + 2]];
                    int first = Slice(planes, Mathf.Min(a.Position.y, Mathf.Min(b.Position.y, c.Position.y)));
                    int last = Slice(planes, Mathf.Max(a.Position.y, Mathf.Max(b.Position.y, c.Position.y)));
                    for (int strip = first; strip <= last; strip++)
                    {
                        polygon.Clear();
                        polygon.Add(a);
                        polygon.Add(b);
                        polygon.Add(c);
                        if (strip > 0) Clip(polygon, clipped, planes[strip - 1], true);
                        if (strip < planes.Length) Clip(polygon, clipped, planes[strip], false);
                        for (int vertex = 1; vertex + 1 < polygon.Count; vertex++)
                        {
                            var p = polygon[0];
                            var q = polygon[vertex];
                            var r = polygon[vertex + 1];
                            if (Vector3.Cross(q.Position - p.Position, r.Position - p.Position).sqrMagnitude < 1e-16f)
                                continue;
                            indices.Add(output.Count);
                            indices.Add(output.Count + 1);
                            indices.Add(output.Count + 2);
                            output.Add(p);
                            output.Add(q);
                            output.Add(r);
                        }
                    }
                }
                submeshes.Add(indices.ToArray());
            }

            flatPositions = output.ConvertAll(v => v.Position).ToArray();
            flatNormals = output.ConvertAll(v => v.Normal).ToArray();
            positions = new Vector3[output.Count];
            normals = new Vector3[output.Count];
            Mesh = new Mesh
            {
                name = source.name + " (folding paper)",
                indexFormat = IndexFormat.UInt32,
                vertices = flatPositions,
                normals = flatNormals,
                uv = output.ConvertAll(v => v.UV).ToArray(),
                subMeshCount = submeshes.Count
            };
            for (int i = 0; i < submeshes.Count; i++) Mesh.SetTriangles(submeshes[i], i);
            Mesh.RecalculateTangents();
            Mesh.MarkDynamic();
            model.transform.localRotation = Quaternion.identity;
            model.sharedMesh = Mesh;
        }

        static int Slice(float[] planes, float y)
        {
            int index = Array.BinarySearch(planes, y);
            return index < 0 ? ~index : index + 1;
        }

        static void Clip(List<Vertex> polygon, List<Vertex> clipped, float height, bool above)
        {
            if (polygon.Count == 0) return;
            clipped.Clear();
            var previous = polygon[polygon.Count - 1];
            bool previousInside = above ? previous.Position.y >= height : previous.Position.y <= height;
            foreach (var current in polygon)
            {
                bool currentInside = above ? current.Position.y >= height : current.Position.y <= height;
                if (currentInside != previousInside)
                {
                    float t = (height - previous.Position.y) / (current.Position.y - previous.Position.y);
                    clipped.Add(Vertex.Lerp(previous, current, t));
                }
                if (currentInside) clipped.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            polygon.Clear();
            polygon.AddRange(clipped);
        }

        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        static Vector2 Arc(float angle, float curvature, float length)
        {
            if (Mathf.Abs(curvature) < .0001f) return length * Direction(angle);
            // The arc keeps the center of the cardboard at its original length.
            float halfTurn = curvature * length * .5f;
            return (2 * Mathf.Sin(halfTurn) / curvature) * Direction(angle + halfTurn);
        }

        void PoseCreases(Vector3 angles)
        {
            Vector2 center = Vector2.zero;
            float angle = 0;
            float height = 0;
            for (int i = 0; i < creases.Length; i++)
            {
                var crease = creases[i];
                center += Direction(angle) * (crease.x - height);
                pose[i] = new CreasePose(center, crease.y, angle, angles[i] * Mathf.Deg2Rad);
                center = pose[i].End;
                angle = pose[i].EndAngle;
                height = crease.x + crease.y;
            }
        }

        Vector2 Centerline(float height, out float angle)
        {
            for (int i = 0; i < creases.Length; i++)
            {
                var crease = creases[i];
                float along = height - crease.x;
                if (along > crease.y) continue;
                var fold = pose[i];
                angle = fold.StartAngle + fold.Curvature * Mathf.Max(0, along);
                return fold.Start + Arc(fold.StartAngle, along < 0 ? 0 : fold.Curvature, along);
            }
            int last = creases.Length - 1;
            angle = pose[last].EndAngle;
            return pose[last].End + Direction(angle) * (height - creases[last].x - creases[last].y);
        }

        public float GroundLift(Quaternion orientation)
        {
            Vector3 up = Quaternion.Inverse(orientation) * Vector3.up;
            var vertices = previousAngles.Equals(Vector3.zero) ? flatPositions : positions;
            float lowest = 0;
            foreach (var vertex in vertices) lowest = Mathf.Min(lowest, Vector3.Dot(vertex, up));
            return -lowest;
        }

        public void Apply(float lower, float waist, float upper)
        {
            var angles = new Vector3(lower, waist, upper);
            if (angles.Equals(previousAngles)) return;
            previousAngles = angles;
            if (angles.Equals(Vector3.zero))
            {
                Array.Copy(flatPositions, positions, positions.Length);
                Array.Copy(flatNormals, normals, normals.Length);
            }
            else
            {
                PoseCreases(angles);
                for (int i = 0; i < positions.Length; i++)
                {
                    var p = flatPositions[i];
                    var n = flatNormals[i];
                    var line = Centerline(p.y, out float angle);
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);
                    positions[i] = new Vector3(p.x, line.x - p.z * sin, line.y + p.z * cos);
                    normals[i] = new Vector3(n.x, n.y * cos - n.z * sin, n.y * sin + n.z * cos).normalized;
                }
            }

            Mesh.vertices = positions;
            Mesh.normals = normals;
            Mesh.RecalculateTangents();
            Mesh.RecalculateBounds();
        }
    }
}
