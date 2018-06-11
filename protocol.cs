using System;
using System.Threading.Tasks;

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
}

public interface IProtocolWriter
{
    ITransport Transport { get; }
    IProtocol Protocol { get; }

    void WriteStringValue(string value);
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
}