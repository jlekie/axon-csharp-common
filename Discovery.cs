using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Axon
{
    public interface IAnnouncer
    {
        string Identifier { get; }

        Task Register(IEncodableEndpoint endpoint);
    }

    public abstract class AAnnouncer : IAnnouncer
    {
        private string identifier;
        public string Identifier
        {
            get
            {
                return this.identifier;
            }
        }

        public AAnnouncer(string identifier)
        {
            this.identifier = identifier;
        }

        public abstract Task Register(IEncodableEndpoint endpoint);
    }

    public interface IDiscoverer<TEndpoint> where TEndpoint : IEndpoint
    {
        string Identifier { get; }
        IEndpointDecoder<TEndpoint> EndpointDecoder { get; }

        Task<TEndpoint> Discover(int timeout = 0);
        Task<TEndpoint[]> DiscoverAll(int timeout = 0);

        Task Blacklist(TEndpoint endpoint);
    }

    public abstract class ADiscoverer<TEndpoint> : IDiscoverer<TEndpoint> where TEndpoint : IEndpoint
    {
        private string identifier;
        public string Identifier
        {
            get
            {
                return this.identifier;
            }
        }

        private IEndpointDecoder<TEndpoint> endpointDecoder;
        public IEndpointDecoder<TEndpoint> EndpointDecoder
        {
            get
            {
                return this.endpointDecoder;
            }
        }

        public ADiscoverer(string identifier, IEndpointDecoder<TEndpoint> endpointDecoder)
        {
            this.identifier = identifier;
            this.endpointDecoder = endpointDecoder;
        }

        public abstract Task<TEndpoint> Discover(int timeout = 0);
        public abstract Task<TEndpoint[]> DiscoverAll(int timeout = 0);

        public abstract Task Blacklist(TEndpoint endpoint);
    }
}