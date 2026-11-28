#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    // Only generated ingredient geometry crosses the connection. Its pose uses regular snapshots.
    internal static class FactoryCoopKitchenMesh
    {
        const int VertexLimit = 100000;
        const int IndexLimit = 600000;

        public static string Encode(Mesh mesh)
        {
            using var bytes = new MemoryStream();
            using (var zip = new DeflateStream(bytes, System.IO.Compression.CompressionLevel.Fastest, true))
            using (var writer = new BinaryWriter(zip))
            {
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uv = mesh.uv;
                writer.Write(vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Write(writer, vertices[i]);
                    Write(writer, i < normals.Length ? normals[i] : Vector3.up);
                    var point = i < uv.Length ? uv[i] : Vector2.zero;
                    writer.Write(point.x);
                    writer.Write(point.y);
                }
                writer.Write(mesh.subMeshCount);
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var triangles = mesh.GetTriangles(i);
                    writer.Write(triangles.Length);
                    foreach (int index in triangles) writer.Write(index);
                }
            }
            return Convert.ToBase64String(bytes.ToArray());
        }

        public static Mesh Decode(string data)
        {
            try
            {
                using var bytes = new MemoryStream(Convert.FromBase64String(data));
                using var zip = new DeflateStream(bytes, CompressionMode.Decompress);
                using var reader = new BinaryReader(zip);
                int count = reader.ReadInt32();
                if (count < 3 || count > VertexLimit) return null;
                var vertices = new Vector3[count];
                var normals = new Vector3[count];
                var uv = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = Read(reader);
                    normals[i] = Read(reader);
                    uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    if (!(vertices[i].sqrMagnitude < 100)) return null;
                }
                int sections = reader.ReadInt32();
                if (sections < 1 || sections > 16) return null;
                var triangles = new int[sections][];
                int total = 0;
                for (int section = 0; section < sections; section++)
                {
                    int length = reader.ReadInt32();
                    if (length < 0 || length > IndexLimit - total || length % 3 != 0) return null;
                    total += length;
                    triangles[section] = new int[length];
                    for (int i = 0; i < length; i++)
                    {
                        int index = reader.ReadInt32();
                        if (index < 0 || index >= count) return null;
                        triangles[section][i] = index;
                    }
                }
                var mesh = new Mesh { indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uv;
                mesh.subMeshCount = sections;
                for (int i = 0; i < sections; i++) mesh.SetTriangles(triangles[i], i, false);
                mesh.RecalculateBounds();
                return mesh;
            }
            catch (Exception error) when (error is IOException || error is FormatException)
            {
                return null;
            }
        }

        static void Write(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
        static Vector3 Read(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
#endif
