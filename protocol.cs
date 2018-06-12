using System;
using System.Threading.Tasks;

namespace Axon
{
    public interface IProtocol
    {
        Task WriteData(Action<IProtocolWriter> handler);
        Task<TResult> ReadData<TResult>(Func<IProtocolReader, TResult> handler);
    }

    public abstract class AProtocol : IProtocol
    {
        public abstract Task WriteData(Action<IProtocolWriter> handler);
        public abstract Task<TResult> ReadData<TResult>(Func<IProtocolReader, TResult> handler);
    }

    public interface IProtocolReader
    {
        ITransport Transport { get; }
        IProtocol Protocol { get; }

        string ReadStringValue();
        bool ReadBooleanValue();
        byte ReadByteValue();
        short ReadShortValue();
        int ReadIntegerValue();
        long ReadLongValue();
        float ReadFloatValue();
        double ReadDoubleValue();
        T ReadEnumValue<T>() where T : struct, IConvertible;
        object ReadIndefinateValue();

        RequestHeader ReadRequestHeader();
        RequestArgumentHeader ReadRequestArgumentHeader();
        ResponseHeader ReadResponseHeader();
        ModelHeader ReadModelHeader();
        ModelPropertyHeader ReadModelPropertyHeader();
        ArrayHeader ReadArrayHeader();
        ArrayItemHeader ReadArrayItemHeader();
    }

    public abstract class AProtocolReader : IProtocolReader
    {
        private readonly ITransport transport;
        public ITransport Transport
        {
            get
            {
                return this.transport;
            }
        }

        private readonly IProtocol protocol;
        public IProtocol Protocol
        {
            get
            {
                return this.protocol;
            }
        }

        public AProtocolReader(ITransport transport, IProtocol protocol)
        {
            this.transport = transport;
            this.protocol = protocol;
        }

        public abstract string ReadStringValue();
        public abstract bool ReadBooleanValue();
        public abstract byte ReadByteValue();
        public abstract short ReadShortValue();
        public abstract int ReadIntegerValue();
        public abstract long ReadLongValue();
        public abstract float ReadFloatValue();
        public abstract double ReadDoubleValue();
        public abstract T ReadEnumValue<T>() where T : struct, IConvertible;
        public abstract object ReadIndefinateValue();

        public RequestHeader ReadRequestHeader()
        {
            var actionName = this.ReadStringValue();
            var argumentCount = this.ReadIntegerValue();

            return new RequestHeader(actionName, argumentCount);
        }
        public RequestArgumentHeader ReadRequestArgumentHeader()
        {
            var argumentName = this.ReadStringValue();
            var type = this.ReadStringValue();

            return new RequestArgumentHeader(argumentName, type);
        }
        public ResponseHeader ReadResponseHeader()
        {
            var success = this.ReadBooleanValue();

            return new ResponseHeader(success);
        }
        public ModelHeader ReadModelHeader()
        {
            var modelName = this.ReadStringValue();
            var propertyCount = this.ReadIntegerValue();

            return new ModelHeader(modelName, propertyCount);
        }
        public ModelPropertyHeader ReadModelPropertyHeader()
        {
            var propertyName = this.ReadStringValue();
            var type = this.ReadStringValue();

            return new ModelPropertyHeader(propertyName, type);
        }
        public ArrayHeader ReadArrayHeader()
        {
            var itemCount = this.ReadIntegerValue();

            return new ArrayHeader(itemCount);
        }
        public ArrayItemHeader ReadArrayItemHeader()
        {
            var type = this.ReadStringValue();

            return new ArrayItemHeader(type);
        }
    }

    public interface IProtocolWriter
    {
        ITransport Transport { get; }
        IProtocol Protocol { get; }

        void WriteStringValue(string value);
        void WriteBooleanValue(bool value);
        void WriteByteValue(byte value);
        void WriteShortValue(short value);
        void WriteIntegerValue(int value);
        void WriteLongValue(long value);
        void WriteFloatValue(float value);
        void WriteDoubleValue(double value);
        void WriteEnumValue<T>(T value) where T : struct, IConvertible;

        void WriteRequestHeader(RequestHeader header);
        void WriteRequestArgumentHeader(RequestArgumentHeader header);
        void WriteResponseHeader(ResponseHeader header);
        void WriteModelHeader(ModelHeader header);
        void WriteModelPropertyHeader(ModelPropertyHeader header);
        void WriteArrayHeader(ArrayHeader header);
        void WriteArrayItemHeader(ArrayItemHeader header);
    }

    public abstract class AProtocolWriter : IProtocolWriter
    {
        private readonly ITransport transport;
        public ITransport Transport
        {
            get
            {
                return this.transport;
            }
        }

        private readonly IProtocol protocol;
        public IProtocol Protocol
        {
            get
            {
                return this.protocol;
            }
        }

        public AProtocolWriter(ITransport transport, IProtocol protocol)
        {
            this.transport = transport;
            this.protocol = protocol;
        }

        public abstract void WriteStringValue(string value);
        public abstract void WriteBooleanValue(bool value);
        public abstract void WriteByteValue(byte value);
        public abstract void WriteShortValue(short value);
        public abstract void WriteIntegerValue(int value);
        public abstract void WriteLongValue(long value);
        public abstract void WriteFloatValue(float value);
        public abstract void WriteDoubleValue(double value);
        public abstract void WriteEnumValue<T>(T value) where T : struct, IConvertible;

        public void WriteRequestHeader(RequestHeader header)
        {
            this.WriteStringValue(header.ActionName);
            this.WriteIntegerValue(header.ArgumentCount);
        }
        public void WriteRequestArgumentHeader(RequestArgumentHeader header)
        {
            this.WriteStringValue(header.ArgumentName);
            this.WriteStringValue(header.Type);
        }
        public void WriteResponseHeader(ResponseHeader header)
        {
            this.WriteBooleanValue(header.Success);
        }
        public void WriteModelHeader(ModelHeader header)
        {
            this.WriteStringValue(header.ModelName);
            this.WriteIntegerValue(header.PropertyCount);
        }
        public void WriteModelPropertyHeader(ModelPropertyHeader header)
        {
            this.WriteStringValue(header.PropertyName);
            this.WriteStringValue(header.Type);
        }
        public void WriteArrayHeader(ArrayHeader header)
        {
            this.WriteIntegerValue(header.ItemCount);
        }
        public void WriteArrayItemHeader(ArrayItemHeader header)
        {
            this.WriteStringValue(header.Type);
        }
    }
}