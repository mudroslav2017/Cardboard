using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cardboard
{
    internal sealed class R82C4916Model
    {
        const string Id = "R82C4916";
        const int IvSize = 16;
        const int Version = 1;
        // The player ships with the key. This keeps the source geometry out of the asset browser.
        const string Key = "/MP6U8Vi+zvEhaY52iyvB7NLUA0cfsvW+toM+Il/oc4=";

        internal Mesh Mesh { get; }
        internal Material[] Surfaces { get; }
        internal Material Trace { get; }
        internal Material Paper { get; }
        internal Material Sleeve { get; }
        internal Vector3 Muzzle => new Vector3(0f, .018f, Mesh.bounds.max.z);

        R82C4916Model(Mesh mesh, Shader shader)
        {
            Mesh = mesh;
            Surfaces = new[]
            {
                Surface(shader, new Color(.31f, .36f, .39f), .42f, .32f),
                Surface(shader, new Color(.20f, .25f, .27f), .30f, .25f)
            };
            Paper = Surface(shader, new Color(.72f, .55f, .35f), 0f, .08f);
            Sleeve = Surface(shader, new Color(.29f, .36f, .29f), 0f, .08f);
            Trace = Surface(shader, new Color(.52f, .92f, 1f), 0f, 0f);
            Trace.EnableKeyword("_EMISSION");
            Trace.SetColor("_EmissionColor", new Color(.28f, .83f, 1f) * 1.8f);
        }

        static Material Surface(Shader shader, Color color, float metallic, float smoothness)
        {
            var surface = new Material(shader) { name = Id, hideFlags = HideFlags.HideAndDontSave };
            surface.SetColor("_BaseColor", color);
            surface.SetFloat("_Metallic", metallic);
            surface.SetFloat("_Smoothness", smoothness);
            return surface;
        }

        internal static R82C4916Model Load(Shader shader)
        {
            var source = Resources.Load<TextAsset>(Id);
            if (!source || !shader)
            {
                Debug.LogWarning(Id + ": missing visual resource or shader.");
                return null;
            }
            byte[] data = source.bytes;
            Resources.UnloadAsset(source);
            try
            {
                using var raw = new MemoryStream(data, false);
                var iv = new byte[IvSize];
                if (raw.Read(iv, 0, iv.Length) != iv.Length) throw new InvalidDataException();
                using var aes = new AesManaged { Key = Convert.FromBase64String(Key), IV = iv };
                using var decryptor = aes.CreateDecryptor();
                using var encrypted = new CryptoStream(raw, decryptor, CryptoStreamMode.Read);
                using var packed = new GZipStream(encrypted, CompressionMode.Decompress);
                using var reader = new BinaryReader(packed);
                return new R82C4916Model(ReadMesh(reader), shader);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException ||
                error is CryptographicException || error is NotSupportedException)
            {
                Debug.LogWarning(Id + ": " + error.Message);
                return null;
            }
        }

        static Mesh ReadMesh(BinaryReader reader)
        {
            if (reader.ReadInt32() != Version) throw new InvalidDataException("Unknown resource version.");
            int count = reader.ReadInt32();
            if (count < 3 || count > ushort.MaxValue) throw new InvalidDataException("Invalid vertex count.");
            var positions = ReadVectors(reader, count);
            var normals = ReadVectors(reader, count);
            if (reader.ReadInt32() != 2) throw new InvalidDataException("Invalid surface count.");
            var faces = new[] { ReadFaces(reader, count), ReadFaces(reader, count) };

            var mesh = new Mesh { name = Id, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.subMeshCount = faces.Length;
            for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i, false);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        static Vector3[] ReadVectors(BinaryReader reader, int count)
        {
            var vectors = new Vector3[count];
            for (int i = 0; i < count; i++)
                vectors[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            return vectors;
        }

        static int[] ReadFaces(BinaryReader reader, int vertices)
        {
            int count = reader.ReadInt32();
            if (count < 3 || count > 400000 || count % 3 != 0) throw new InvalidDataException("Invalid face count.");
            var indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = reader.ReadUInt16();
                if (indices[i] >= vertices) throw new InvalidDataException("Invalid vertex index.");
            }
            return indices;
        }

        internal void Release()
        {
            Object.Destroy(Mesh);
            foreach (var surface in Surfaces) Object.Destroy(surface);
            Object.Destroy(Trace);
            Object.Destroy(Paper);
            Object.Destroy(Sleeve);
        }
    }
}
