using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Axon
{
    public interface ITcpTransport : ITransport
    {
    }

    public interface ITcpServerTransport : IServerTransport
    {
    }

    public interface ITcpClientTransport : IClientTransport
    {
    }

    public class TcpServerTransport : AServerTransport, ITcpServerTransport
    {
        private readonly ConcurrentQueue<Message> ReceiveBuffer;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<Message>> SendBuffer;

        private Task ListeningTask;

        public TcpServerTransport()
        {
            this.ReceiveBuffer = new ConcurrentQueue<Message>();
            this.SendBuffer = new ConcurrentDictionary<string, ConcurrentQueue<Message>>();
        }

        public override Task Listen(IEndpoint endpoint)
        {
            // this.ListeningTask = this.ServerHandler();
            this.ListeningTask = Task.Run(() => this.ServerHandler());

            return Task.CompletedTask;
        }

        public override Task Send(byte[] data, IDictionary<string, byte[]> metadata)
        {
            var frames = new Dictionary<string, byte[]>();
            foreach (var key in metadata.Keys)
                frames.Add(key, metadata[key]);
            frames.Add("payload", data);

            byte[] identityData;
            if (!frames.TryGetValue("identity", out identityData))
                throw new Exception("Identity metadata missing");
            var identity = System.Text.Encoding.ASCII.GetString(identityData);

            var message = new Message(frames);
            this.SendBuffer[identity].Enqueue(message);

            return Task.CompletedTask;
        }
        public override async Task<ReceivedData> Receive()
        {
            var message = await this.GetBufferedData();

            var payloadData = message.Frames["payload"];
            var metadata = message.Frames.Where(f => f.Key != "payload").ToDictionary(p => p.Key, p => p.Value);

            this.OnDataReceived(payloadData, metadata);

            return new ReceivedData(payloadData, metadata);
        }
        public override Task<Func<Task<ReceivedData>>> SendAndReceive(byte[] data, IDictionary<string, byte[]> metadata)
        {
            throw new NotImplementedException();
        }

        private async Task ServerHandler()
        {
            var server = new TcpListener(IPAddress.Loopback, 3333);
            server.Start();

            while (true)
            {
                // Console.WriteLine("Waiting for client...");

                var client = await server.AcceptTcpClientAsync();
                // Console.WriteLine("Client connected");

                var task = Task.Factory.StartNew(() => this.ServerClientHandler(client), TaskCreationOptions.LongRunning);
            }
        }

        private Task ServerClientHandler(TcpClient client)
        {
            bool connected = client.Connected;

            using (var stream = client.GetStream())
            using (var streamDecoder = new BinaryReader(stream))
            {
                var identifier = streamDecoder.ReadString();
                this.SendBuffer.TryAdd(identifier, new ConcurrentQueue<Message>());

                while (connected)
                {
                    try
                    {
                        while (stream.DataAvailable)
                        {
                            var message = streamDecoder.ReadMessage();

                            // Console.WriteLine("Received Message");
                            // foreach (var frame in message.Frames)
                            //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                            this.ReceiveBuffer.Enqueue(message);
                        }

                        this.SendBufferedData(identifier, stream);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }

                    byte[] checkBuffer = new byte[1];
                    connected = !(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Receive(checkBuffer, SocketFlags.Peek) == 0);
                }
            }

            Console.WriteLine("Client disconnected");

            client.Dispose();

            return Task.CompletedTask;
        }

        private async Task<Message> GetBufferedData()
        {
            Message message;
            if (!this.ReceiveBuffer.IsEmpty && this.ReceiveBuffer.TryDequeue(out message))
            {
                // Console.WriteLine("Getting Message");
                // foreach (var frame in message.Frames)
                //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                return message;
            }
            else
            {
                var start = DateTime.Now;

                while (true)
                {
                    if (!this.ReceiveBuffer.IsEmpty && this.ReceiveBuffer.TryDequeue(out message))
                    {
                        // Console.WriteLine("Getting Message");
                        // foreach (var frame in message.Frames)
                        //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                        return message;
                    }
                    else if ((DateTime.Now - start).Milliseconds > 30000)
                    {
                        throw new Exception("Message timeout");
                    }

                    await Task.Delay(5);
                }
            }
        }
        private void SendBufferedData(string identifier, Stream stream)
        {
            Message message;
            while (!this.SendBuffer[identifier].IsEmpty && this.SendBuffer[identifier].TryDequeue(out message))
            {
                var payloadEncoder = new BinaryWriter(stream);

                // Console.WriteLine("Sent Message");
                // foreach (var frame in message.Frames)
                //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                payloadEncoder.WriteMessage(message);
            }
        }
    }

    public class TcpClientTransport : AClientTransport, ITcpClientTransport
    {
        private readonly string identity;
        public string Identity
        {
            get
            {
                return identity;
            }
        }

        private readonly ConcurrentQueue<Message> ReceiveBuffer;
        private readonly ConcurrentQueue<Message> SendBuffer;
        private readonly ConcurrentDictionary<string, Message> TaggedReceiveBuffer;

        private bool IsRunning;
        private Task ListeningTask;

        private object LockContext = new Object();

        public TcpClientTransport()
        {
            this.identity = Guid.NewGuid().ToString().Replace("-", "").ToLowerInvariant();

            this.ReceiveBuffer = new ConcurrentQueue<Message>();
            this.SendBuffer = new ConcurrentQueue<Message>();
            this.TaggedReceiveBuffer = new ConcurrentDictionary<string, Message>();
        }

        public override Task Connect(IEndpoint endpoint)
        {
            this.IsRunning = true;
            // this.ListeningTask = this.ServerHandler();
            this.ListeningTask = Task.Run(() => this.ServerHandler());

            return Task.CompletedTask;
        }
        public async override Task Close()
        {
            this.IsRunning = false;
            await this.ListeningTask;
        }

        public override Task Send(byte[] data, IDictionary<string, byte[]> metadata)
        {
            var encodedIdentity = System.Text.Encoding.ASCII.GetBytes(this.Identity);

            var frames = new Dictionary<string, byte[]>();
            foreach (var key in metadata.Keys)
                frames.Add(key, metadata[key]);
            frames.Add("identity", encodedIdentity);
            frames.Add("payload", data);

            var message = new Message(frames);
            this.SendBuffer.Enqueue(message);

            return Task.CompletedTask;
        }
        public override async Task<ReceivedData> Receive()
        {
            var message = await this.GetBufferedData();

            var payloadData = message.Frames["payload"];
            var metadata = message.Frames.Where(f => f.Key != "payload").ToDictionary(p => p.Key, p => p.Value);

            this.OnDataReceived(payloadData, metadata);

            return new ReceivedData(payloadData, metadata);
        }
        public override Task<Func<Task<ReceivedData>>> SendAndReceive(byte[] data, IDictionary<string, byte[]> metadata)
        {
            var rid = Guid.NewGuid().ToString().Replace("-", "").ToLowerInvariant();
            var encodedRid = System.Text.Encoding.ASCII.GetBytes(rid);

            var encodedIdentity = System.Text.Encoding.ASCII.GetBytes(this.Identity);

            var frames = new Dictionary<string, byte[]>();
            foreach (var key in metadata.Keys)
                frames.Add(key, metadata[key]);
            frames.Add("identity", encodedIdentity);
            frames.Add("rid", encodedRid);
            frames.Add("payload", data);

            var message = new Message(frames);
            this.SendBuffer.Enqueue(message);

            return Task.FromResult(new Func<Task<ReceivedData>>(async () => {
                var responseMessage = await this.GetBufferedTaggedData(rid);

                var responsePayloadData = responseMessage.Frames["payload"];
                var responseMetadata = responseMessage.Frames.Where(f => f.Key != "payload").ToDictionary(p => p.Key, p => p.Value);

                this.OnDataReceived(responsePayloadData, responseMetadata);

                return new ReceivedData(responsePayloadData, responseMetadata);
            }));
        }

        private async Task ServerHandler()
        {
            using (var client = new TcpClient())
            {
                while (this.IsRunning)
                {
                    await client.ConnectAsync("127.0.0.1", 3333);

                    using (var stream = client.GetStream())
                    using (var streamDecoder = new BinaryReader(stream))
                    using (var streamEncoder = new BinaryWriter(stream))
                    {
                        streamEncoder.Write(this.Identity);

                        while (client.Connected && this.IsRunning)
                        {
                            while (stream.DataAvailable)
                            {
                                lock (this.LockContext)
                                {
                                    var message = streamDecoder.ReadMessage();
                                    if (message.Frames.ContainsKey("rid"))
                                    {
                                        var decodedRid = System.Text.Encoding.ASCII.GetString(message.Frames["rid"]);

                                        this.TaggedReceiveBuffer.TryAdd(decodedRid, message);
                                    }
                                    else
                                    {
                                        this.ReceiveBuffer.Enqueue(message);
                                    }
                                }
                            }

                            this.SendBufferedData(stream);
                        }

                        client.Client.Disconnect(false);
                    }
                }

                Console.WriteLine("Closed client");
            }
        }

        private async Task<Message> GetBufferedData()
        {
            Message message;
            // if (!this.ReceiveBuffer.IsEmpty && (rid != null && this.ReceiveBuffer.TryPeek(out message) && message.RID == rid) && this.ReceiveBuffer.TryDequeue(out message))'
            if (!this.ReceiveBuffer.IsEmpty && this.ReceiveBuffer.TryDequeue(out message))
            {
                // Console.WriteLine("Received Message " + DateTime.UtcNow.Ticks);
                // foreach (var frame in message.Frames)
                //     // Console.WriteLine("  " + frame.Key + " [ " + System.Text.Encoding.ASCII.GetString(frame.Value) + " ]");
                //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                return message;
            }
            else
            {
                var start = DateTime.Now;

                while (true)
                {
                    // if (!this.ReceiveBuffer.IsEmpty && (rid != null && this.ReceiveBuffer.TryPeek(out message) && message.RID == rid) && this.ReceiveBuffer.TryDequeue(out message))
                    if (!this.ReceiveBuffer.IsEmpty && this.ReceiveBuffer.TryDequeue(out message))
                    {
                        // Console.WriteLine("Received Message " + DateTime.UtcNow.Ticks);
                        // foreach (var frame in message.Frames)
                        //     // Console.WriteLine("  " + frame.Key + " [ " + System.Text.Encoding.ASCII.GetString(frame.Value) + " ]");
                        //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                        return message;
                    }
                    else if ((DateTime.Now - start).Milliseconds > 30000)
                    {
                        throw new Exception("Message timeout");
                    }

                    await Task.Delay(5);
                }
            }
        }
        private async Task<Message> GetBufferedTaggedData(string rid)
        {
            Message message;
            if (!this.TaggedReceiveBuffer.IsEmpty && this.TaggedReceiveBuffer.TryRemove(rid, out message))
            {
                // Console.WriteLine("Received Tagged Message");
                // foreach (var frame in message.Frames)
                //     // Console.WriteLine("  " + frame.Key + " [ " + System.Text.Encoding.ASCII.GetString(frame.Value) + " ]");
                //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                return message;
            }
            else
            {
                var start = DateTime.Now;

                while (true)
                {
                    if (!this.TaggedReceiveBuffer.IsEmpty && this.TaggedReceiveBuffer.TryRemove(rid, out message))
                    {
                        // Console.WriteLine("Received Tagged Message");
                        // foreach (var frame in message.Frames)
                        //     // Console.WriteLine("  " + frame.Key + " [ " + System.Text.Encoding.ASCII.GetString(frame.Value) + " ]");
                        //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                        return message;
                    }
                    else if ((DateTime.Now - start).Milliseconds > 30000)
                    {
                        throw new Exception("Tagged message timeout");
                    }

                    await Task.Delay(5);
                }
            }
        }
        private void SendBufferedData(Stream stream)
        {
            Message message;
            while (!this.SendBuffer.IsEmpty && this.SendBuffer.TryDequeue(out message))
            {
                lock (this.LockContext)
                {
                    var payloadEncoder = new BinaryWriter(stream);

                    // Console.WriteLine("Sent Message");
                    // foreach (var frame in message.Frames)
                    //     Console.WriteLine("  " + frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");

                    payloadEncoder.WriteMessage(message);
                }
            }
        }
    }

    internal struct Message
    {
        public readonly ReadOnlyDictionary<string, byte[]> Frames;

        public Message(IDictionary<string ,byte[]> frames)
        {
            this.Frames = new ReadOnlyDictionary<string, byte[]>(frames);
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var frame in this.Frames)
            {
                sb.AppendLine(frame.Key + " [ " + BitConverter.ToString(frame.Value).Replace("-", " ") + " ]");
            }

            return sb.ToString();
        }
    }

    internal static class BinaryHelpers
    {
        public static void WriteMessage(this BinaryWriter writer, Message message)
        {
            writer.WriteMessage(message.Frames);
        }
        public static void WriteMessage(this BinaryWriter writer, IDictionary<string, byte[]> frames)
        {
            writer.Write('M');
            writer.Write(frames.Count);
            foreach (var frame in frames)
                writer.WriteFrame(frame.Key, frame.Value);
        }
        public static Message ReadMessage(this BinaryReader reader)
        {
            var messageChar = reader.ReadChar();
            if (messageChar != 'M')
                throw new Exception("Invalid message");

            var frames = new Dictionary<string, byte[]>();

            var frameCount = reader.ReadInt32();
            for (var i = 0; i < frameCount; i++)
            {
                var frame = reader.ReadFrame();

                frames.Add(frame.Key, frame.Value);
            }

            return new Message(frames);
        }

        public static void WriteFrame(this BinaryWriter writer, string name, byte[] payloadData)
        {
            writer.Write(name);
            writer.Write(payloadData.Length);
            writer.Write(payloadData);
        }
        public static KeyValuePair<string, byte[]> ReadFrame(this BinaryReader reader)
        {
            var name = reader.ReadString();
            var payloadLength = reader.ReadInt32();
            var payload = reader.ReadBytes(payloadLength);

            return new KeyValuePair<string, byte[]>(name, payload);
        }
    }
}