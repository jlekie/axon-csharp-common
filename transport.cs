using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Axon
{
    public struct ReceivedData
    {
        public readonly byte[] Data;
        public readonly Dictionary<string, byte[]> Metadata;

        public ReceivedData(byte[] data)
        {
            this.Data = data;
            this.Metadata = new Dictionary<string, byte[]>();
        }
        public ReceivedData(byte[] data, Dictionary<string, byte[]> metadata)
        {
            this.Data = data;
            this.Metadata = new Dictionary<string, byte[]>(metadata);
        }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; private set; }
        public Dictionary<string, byte[]> Metadata { get; private set; }

        public DataReceivedEventArgs(byte[] data, Dictionary<string, byte[]> metadata)
            : base()
        {
            this.Data = data;
            this.Metadata = metadata;
        }
    }
    public class DataSentEventArgs : EventArgs
    {
        public byte[] Data { get; private set; }
        public Dictionary<string, byte[]> Metadata { get; private set; }

        public DataSentEventArgs(byte[] data, Dictionary<string, byte[]> metadata)
            : base()
        {
            this.Data = data;
            this.Metadata = metadata;
        }
    }

    public interface ITransport
    {
        event EventHandler<DataReceivedEventArgs> DataReceived;
        event EventHandler<DataSentEventArgs> DataSent;

        Task Send(byte[] data, Dictionary<string, byte[]> metadata);
        Task<ReceivedData> Receive();
    }

    public interface IServerTransport : ITransport {
        Task Listen(IEndpoint endpoint);
    }

    public interface IClientTransport : ITransport {
        Task Connect(IEndpoint endpoint);
    }

    public abstract class ATransport : ITransport
    {
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DataSentEventArgs> DataSent;

        public abstract Task Send(byte[] data, Dictionary<string, byte[]> metadata);
        public abstract Task<ReceivedData> Receive();

        protected virtual void OnDataReceived(byte[] data, Dictionary<string, byte[]> metadata)
        {
            if (this.DataReceived != null)
                this.DataReceived(this, new DataReceivedEventArgs(data, metadata));
        }
        protected virtual void OnDataSent(byte[] data, Dictionary<string, byte[]> metadata)
        {
            if (this.DataSent != null)
                this.DataSent(this, new DataSentEventArgs(data, metadata));
        }
    }

    public abstract class AServerTransport : ATransport, IServerTransport
    {
        public abstract Task Listen(IEndpoint endpoint);
    }
    public abstract class AClientTransport : ATransport, IClientTransport
    {
        public abstract Task Connect(IEndpoint endpoint);
    }
}