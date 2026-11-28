#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cardboard
{
    [Serializable]
    internal struct FactoryPrototypeFrame
    {
        public Vector3 Position;
        public float Yaw, Pitch, Jump;
        public float SentAt, Echo;
        public int AssignedRole, WorldVersion, AcknowledgedWorld;
        public int InputVersion, AcknowledgedInput;
        public FactoryCoopAvatar[] Avatars;
        public string Refusal;
        public int AcknowledgedStrokes, AcknowledgedRound, AcknowledgedKitchenMesh;
        public bool Left, Right;
        public FactoryCoopControls Controls;
        public FactoryCoopCommand[] Commands;
        public FactoryCoopWorld World;

        public Ray Aim => new Ray(Position + Vector3.up * (1.62f + Jump),
            Quaternion.Euler(Pitch, Yaw, 0) * Vector3.forward);
    }

    // A versioned header and a bounded payload. TCP may split either across reads.
    internal sealed class FactoryPrototypeConnection : IDisposable
    {
        const int Protocol = 0x33444243; // CBD3
        const int HeaderSize = sizeof(int) * 2;
        const int MaximumPayload = 2 * 1024 * 1024;
        const int BufferSize = HeaderSize + MaximumPayload;
        readonly TcpClient client;
        readonly NetworkStream stream;
        readonly byte[] incoming = new byte[BufferSize];
        readonly byte[] outgoing = new byte[BufferSize];
        readonly BinaryWriter writer;
        readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        readonly SemaphoreSlim receiveSlots = new SemaphoreSlim(32);
        readonly CancellationTokenSource closing = new CancellationTokenSource();
        readonly Task read;
        Task write;
        public int LastPacketBytes { get; private set; }
        public bool ReadyToSend => write == null || write.IsCompleted;

        public FactoryPrototypeConnection(TcpClient client)
        {
            this.client = client;
            client.NoDelay = true;
            client.SendBufferSize = 12 * 1024;
            stream = client.GetStream();
            writer = new BinaryWriter(new MemoryStream(outgoing, true));
            read = ReadMessages(closing.Token);
        }

        async Task ReadMessages(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                await receiveSlots.WaitAsync(cancellation).ConfigureAwait(false);
                messages.Enqueue(await ReadMessage().ConfigureAwait(false));
            }
        }

        async Task ReadExactly(int length)
        {
            int offset = 0;
            while (offset < length)
            {
                int count = await stream.ReadAsync(incoming, offset, length - offset).ConfigureAwait(false);
                if (count == 0) throw new IOException("The other player disconnected.");
                offset += count;
            }
        }

        async Task<string> ReadMessage()
        {
            await ReadExactly(HeaderSize).ConfigureAwait(false);
            using var header = new BinaryReader(new MemoryStream(incoming, 0, HeaderSize, false));
            if (header.ReadInt32() != Protocol) throw new IOException("The other player is using an incompatible build.");
            int encodedLength = header.ReadInt32();
            if (encodedLength == 0 || encodedLength > MaximumPayload || encodedLength < -MaximumPayload)
                throw new IOException("Invalid multiplayer message length.");
            int length = Math.Abs(encodedLength);
            await ReadExactly(length).ConfigureAwait(false);
            if (encodedLength > 0) return Encoding.UTF8.GetString(incoming, 0, length);
            using var compressed = new MemoryStream(incoming, 0, length, false);
            using var zip = new DeflateStream(compressed, CompressionMode.Decompress);
            using var decoded = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (decoded.Length + count > MaximumPayload) throw new IOException("The multiplayer message is too large.");
                decoded.Write(buffer, 0, count);
            }
            return Encoding.UTF8.GetString(decoded.GetBuffer(), 0, (int)decoded.Length);
        }

        public bool TryReceive(out FactoryPrototypeFrame frame)
        {
            frame = default;
            if (!messages.TryDequeue(out var message))
            {
                if (read.IsCompleted) read.GetAwaiter().GetResult();
                return false;
            }
            receiveSlots.Release();
            try
            {
                frame = JsonUtility.FromJson<FactoryPrototypeFrame>(message);
            }
            catch (ArgumentException error)
            {
                throw new IOException("Invalid multiplayer message.", error);
            }
            return true;
        }

        public bool Send(FactoryPrototypeFrame frame)
        {
            if (write != null)
            {
                if (!write.IsCompleted) return false;
                write.GetAwaiter().GetResult();
            }

            string json = JsonUtility.ToJson(frame);
            var encoding = Encoding.UTF8;
            int bytes = encoding.GetByteCount(json);
            if (bytes > MaximumPayload) throw new IOException("The multiplayer message is too large.");
            int length = bytes;
            if (bytes >= 512)
            {
                var plain = encoding.GetBytes(json);
                using var compressed = new MemoryStream();
                using (var zip = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
                    zip.Write(plain, 0, plain.Length);
                if (compressed.Length < bytes)
                {
                    length = -(int)compressed.Length;
                    Buffer.BlockCopy(compressed.GetBuffer(), 0, outgoing, HeaderSize, -length);
                }
                else Buffer.BlockCopy(plain, 0, outgoing, HeaderSize, bytes);
            }
            else encoding.GetBytes(json, 0, json.Length, outgoing, HeaderSize);
            writer.BaseStream.Position = 0;
            writer.Write(Protocol);
            writer.Write(length);
            LastPacketBytes = HeaderSize + Math.Abs(length);
            write = stream.WriteAsync(outgoing, 0, LastPacketBytes);
            return true;
        }

        public void SendAndClose(FactoryPrototypeFrame frame)
        {
            try
            {
                Send(frame);
                Task.WhenAny(write, Task.Delay(500)).ContinueWith(_ => Dispose(), TaskScheduler.Default);
            }
            catch (Exception error) when (error is IOException || error is SocketException)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            closing.Cancel();
            client.Dispose();
            writer.Dispose();
        }
    }
}
#endif
