using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Axon
{
    public interface IEndpoint
    {
    }
    public interface IServerEndpoint : IEndpoint
    {
    }
    public interface IClientEndpoint : IEndpoint
    {
    }

    public abstract class AServerEndpoint : IServerEndpoint
    {
    }
    public abstract class AClientEndpoint : IClientEndpoint
    {
    }

    public interface IEndpointDecoder<out TEndpoint> where TEndpoint : IEndpoint
    {
        TEndpoint Decode(byte[] payload);
    }

    public abstract class AEndpointDecoder<TEndpoint> : IEndpointDecoder<TEndpoint> where TEndpoint : IEndpoint
    {
        public abstract TEndpoint Decode(byte[] payload);
    }

    public interface IEncodableEndpoint : IEndpoint
    {
        byte[] Encode();
    }
}