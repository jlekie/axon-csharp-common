//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.IO;

//namespace Axon
//{
//    //internal static class DateTimeHelpers
//    //{
//    //    public static long ToUnixTimestamp(this DateTime dateTime)
//    //    {
//    //        return (long)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
//    //    }
//    //}

//    public interface IServiceDiscoveryServer
//    {
//        IServerTransport Transport { get; }
//        IProtocol Protocol { get; }

//        Task Start();
//        Task Run();
//    }
//    public class ServiceDiscoveryServer : IServiceDiscoveryServer
//    {
//        private readonly IServerTransport transport;
//        public IServerTransport Transport
//        {
//            get
//            {
//                return this.transport;
//            }
//        }

//        private readonly IProtocol protocol;
//        public IProtocol Protocol
//        {
//            get
//            {
//                return this.protocol;
//            }
//        }

//        private Task RunningTask;

//        public ServiceDiscoveryServer(IServerTransport transport, IProtocol protocol)
//        {
//            this.transport = transport;
//            this.protocol = protocol;
//        }

//        public async Task Start()
//        {
//            await this.Transport.Listen();

//            // this.IsRunning = true;
//            this.RunningTask = Task.Run(() => this.ServerHandler());
//            // this.RunningTask = Task.Factory.StartNew(() => this.ServerHandler(), TaskCreationOptions.LongRunning);

//            if (this.Announcer != null)
//            {
//                Console.WriteLine("Registering endpoint");
//                await this.Announcer.Register(this.Endpoint);

//                var tmp = Task.Run(() => this.AnnouncerHandler());
//            }

//            // return Task.FromResult(true);
//        }
//        public async Task Run()
//        {
//            await this.Start();

//            await this.RunningTask;
//            Console.WriteLine(this.RunningTask.Status);
//        }

//        protected abstract Task HandleRequest();

//        private async Task ServerHandler()
//        {
//            // await this.AnnouncerHandler();

//            while (true)
//            {
//                try
//                {
//                    await this.HandleRequest();
//                }
//                catch (Exception ex)
//                {
//                    Console.Error.WriteLine(ex.Message);
//                }
//                // System.Threading.Thread.Sleep(1000);
//            }
//        }
//        private async Task AnnouncerHandler()
//        {
//            // await this.Announcer.Register(this.Transport.Endpoint);
//            while (true)
//            {
//                System.Threading.Thread.Sleep(10000);

//                try
//                {
//                    Console.WriteLine("Registering endpoint");
//                    await this.Announcer.Register(this.Endpoint);
//                }
//                catch (Exception ex)
//                {
//                    Console.Error.WriteLine(ex.Message);
//                }
//            }
//        }
//    }

//    public interface IMemoryMappedDiscoverer<TEndpoint> : IDiscoverer<TEndpoint> where TEndpoint : IEndpoint
//    {
//        string MapPath { get; }
//        string MapName { get; }
//    }

//    public class MemoryMappedDiscoverer<TEndpoint> : ADiscoverer<TEndpoint>, IMemoryMappedDiscoverer<TEndpoint> where TEndpoint : IEndpoint
//    {
//        private string mapPath;
//        public string MapPath
//        {
//            get
//            {
//                return this.mapPath;
//            }
//        }

//        private string mapName;
//        public string MapName
//        {
//            get
//            {
//                return this.mapName;
//            }
//        }

//        public MemoryMappedDiscoverer(string identifier, IEndpointDecoder<TEndpoint> endpointDecoder, string mapPath, string mapName)
//            : base(identifier, endpointDecoder)
//        {
//            this.mapPath = mapPath;
//            this.mapName = mapName;
//        }

//        public async override Task<TEndpoint> Discover()
//        {
//            // using (System.IO.File.Open(this.MapPath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite)) { }

//            using (var fs = new FileStream(this.MapPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
//#if NETSTANDARD
//            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
//#else
//            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false))
//#endif
//            // using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(this.MapPath, System.IO.FileMode.Open, this.MapName, 1024 * 32))
//            using (var accessor = mmf.CreateViewAccessor(0, 1024 * 32))
//            {
//                var databaseAccessor = new MemoryMappedDatabaseAccessor(accessor);

//                var records = databaseAccessor.ResolveRecords(this.Identifier);
//                while (records.Length <= 0)
//                {
//                    await Task.Delay(500);

//                    records = databaseAccessor.ResolveRecords(this.Identifier);
//                }
//                var rand = new Random();

//                var encodedPayload = records[rand.Next(0, records.Length)];

//                return this.EndpointDecoder.Decode(encodedPayload);
//            }
//        }
//    }
//}