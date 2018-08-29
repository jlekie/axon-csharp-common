using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Axon
{
    internal static class DateTimeHelpers
    {
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            return (long)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }
    }

    public struct MemoryMappedDatabaseHeader
    {
        public int RecordCount;
        public bool Locked;
    }

    public class MemoryMappedDatabaseRecord
    {
        ////public Guid Id;
        public long Timestamp;
        public byte[] Payload;

        public int Offset;
    }

    public class MemoryMappedDatabaseAccessor
    {
        public static readonly int HEADER_SIZE = sizeof(int) + sizeof(bool);
        public static readonly int RECORD_SIZE = 50 + sizeof(long) + 250;

        public MemoryMappedViewAccessor ViewAccessor
        {
            get;
            private set;
        }

        public MemoryMappedDatabaseAccessor(MemoryMappedViewAccessor viewAccessor)
        {
            this.ViewAccessor = viewAccessor;
        }

        public void InitializeDatabase()
        {
            Console.WriteLine("Initializing MMF");

            this.ViewAccessor.Write(0, 0);
            this.ViewAccessor.Write(sizeof(int) + 1, false);
        }

        public MemoryMappedDatabaseHeader ResolveHeader()
        {
            var recordCount = this.ViewAccessor.ReadInt32(0);
            var locked = this.ViewAccessor.ReadBoolean(sizeof(int) + 1);

            return new MemoryMappedDatabaseHeader() { RecordCount = recordCount, Locked = locked };
        }
        public MemoryMappedDatabaseRecord[] ResolveRecords(string identifier)
        {
            var resolvedRecords = new List<MemoryMappedDatabaseRecord>();

            var header = this.ResolveHeader();
            for (var i = HEADER_SIZE + 1; i < header.RecordCount * RECORD_SIZE; i += RECORD_SIZE)
            {
                byte[] encodedIdentifier = new byte[50];
                this.ViewAccessor.ReadArray(i, encodedIdentifier, 0, encodedIdentifier.Length);
                string recordIdentifier = System.Text.Encoding.UTF8.GetString(encodedIdentifier.TakeWhile(b => b != 0).ToArray());

                //Console.WriteLine($"  Identifier [{i}]: {recordIdentifier}");

                if (recordIdentifier == identifier)
                {
                    ////// Id
                    ////byte[] encodedId = new byte[16];
                    ////this.ViewAccessor.ReadArray(i + encodedIdentifier.Length, encodedId, 0, encodedId.Length);
                    ////Guid id = new Guid(encodedId);

                    // Timestamp
                    var recordTimestamp = this.ViewAccessor.ReadInt64(i + encodedIdentifier.Length);

                    // Payload
                    byte[] encodedPayload = new byte[250];
                    this.ViewAccessor.ReadArray(i + encodedIdentifier.Length + sizeof(long), encodedPayload, 0, encodedPayload.Length);

                    resolvedRecords.Add(new MemoryMappedDatabaseRecord()
                    {
                        ////Id = id,
                        Timestamp = recordTimestamp,
                        Payload = encodedPayload.TakeWhile(b => b != 0).ToArray(),
                        Offset = i
                    });
                }
            }

            return resolvedRecords.ToArray();
        }

        public void AppendRecord(string identifier, byte[] payload)
        {
            this.AquireLock(identifier, () =>
            {
                Console.WriteLine($"Appending Record for {identifier}");

                var header = this.ResolveHeader();

                //Console.WriteLine($"  Pre-Record Count: {header.RecordCount}");

                // Identifier
                byte[] encodedIdentifier = new byte[50];
                System.Text.Encoding.UTF8.GetBytes(identifier).CopyTo(encodedIdentifier, 0);
                this.ViewAccessor.WriteArray(HEADER_SIZE + header.RecordCount * RECORD_SIZE + 1, encodedIdentifier, 0, encodedIdentifier.Length);

                ////// Id
                ////byte[] encodedId = id.ToByteArray();
                ////this.ViewAccessor.WriteArray(HEADER_SIZE + header.RecordCount * RECORD_SIZE + encodedIdentifier.Length + 1, encodedId, 0, encodedId.Length);

                // Timestamp
                this.ViewAccessor.Write(HEADER_SIZE + header.RecordCount * RECORD_SIZE + encodedIdentifier.Length + 1, DateTime.UtcNow.ToUnixTimestamp());

                // Payload
                byte[] encodedPayload = new byte[250];
                payload.CopyTo(encodedPayload, 0);
                this.ViewAccessor.WriteArray(HEADER_SIZE + header.RecordCount * RECORD_SIZE + encodedIdentifier.Length + sizeof(long) + 1, encodedPayload, 0, encodedPayload.Length);

                // Increment record count
                this.ViewAccessor.Write(0, header.RecordCount + 1);

                header = this.ResolveHeader();

                //System.Threading.Thread.Sleep(5000);

                //Console.WriteLine($"  Post-Record Count: {header.RecordCount}");
            });
        }

        public void UpdateRecord(int offset, string identifier, byte[] payload)
        {
            this.AquireLock(identifier, () =>
            {
                Console.WriteLine($"Updating Record for {identifier} at {offset}");

                var header = this.ResolveHeader();

                // Identifier
                byte[] encodedIdentifier = new byte[50];
                System.Text.Encoding.UTF8.GetBytes(identifier).CopyTo(encodedIdentifier, 0);
                this.ViewAccessor.WriteArray(offset, encodedIdentifier, 0, encodedIdentifier.Length);

                // Timestamp
                this.ViewAccessor.Write(offset + encodedIdentifier.Length, DateTime.UtcNow.ToUnixTimestamp());

                // Payload
                byte[] encodedPayload = new byte[250];
                payload.CopyTo(encodedPayload, 0);
                this.ViewAccessor.WriteArray(offset + encodedIdentifier.Length + sizeof(long), encodedPayload, 0, encodedPayload.Length);

                header = this.ResolveHeader();
            });
        }

        // public void Refresh()
        // {
        //     this.LockDatabase();

        //     var header = this.ResolveHeader();
        //     for (var i = HEADER_SIZE + 1; i < header.RecordCount * RECORD_SIZE; i += RECORD_SIZE)
        //     {
        //         var recordTimestamp = this.ViewAccessor.ReadInt64(i + 50);

        //         if (DateTime.UtcNow.ToUnixTimestamp() - recordTimestamp > 60)
        //         {
        //             byte[] encodedPayload = new byte[250];
        //             this.ViewAccessor.ReadArray(i + encodedIdentifier.Length + sizeof(long), encodedPayload, 0, encodedPayload.Length);

        //             resolvedRecords.Add(encodedPayload.TakeWhile(b => b != 0).ToArray());
        //         }
        //     }

        //     this.UnlockDatabase();
        // }

        private void AquireLock(string identifier, Action handler)
        {
            using (var mutex = new System.Threading.Mutex(false, "mmdtesting"))
            {
                bool mutexAquired;
                try
                {
                    Console.WriteLine($"(waiting for lock context for '{identifier}')");
                    mutexAquired = mutex.WaitOne(30000);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    mutexAquired = true;
                }

                if (!mutexAquired)
                    throw new Exception("Aquire lock timeout");

                try
                {
                    handler();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            //while (this.IsLocked())
            //{
            //    Console.WriteLine($"(waiting for lock context for '{identifier}')");
            //    await Task.Delay(1000);
            //}

            //this.LockDatabase();
            //handler();
            //this.UnlockDatabase();
        }

        private bool IsLocked()
        {
            return this.ViewAccessor.ReadBoolean(sizeof(int) + 1);
        }
        private void LockDatabase()
        {
            this.ViewAccessor.Write(5, true);
        }
        private void UnlockDatabase()
        {
            this.ViewAccessor.Write(5, false);
        }
    }

    public interface IMemoryMappedAnnouncer : IAnnouncer
    {
        string MapName { get; }

        //void Initialize();
    }
    public class MemoryMappedAnnouncer : AAnnouncer, IMemoryMappedAnnouncer
    {
        private readonly string mapName;
        public string MapName
        {
            get
            {
                return this.mapName;
            }
        }

        public MemoryMappedAnnouncer(string identifier, string mapName)
            : base(identifier)
        {
            this.mapName = mapName;
        }

        public override Task Register(IEncodableEndpoint endpoint)
        {
            //            bool isNew = false;
            //            if (!System.IO.File.Exists(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat"))
            //            {
            //                isNew = true;
            //                using (System.IO.File.Open(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite)) { }
            //            }

            //            using (var fs = new FileStream(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            //#if NETSTANDARD
            //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
            //#else
            //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, null, HandleInheritability.None, false))
            //#endif
            using (var mmf = MemoryMappedFile.OpenExisting("Local\\" + this.MapName, MemoryMappedFileRights.ReadWrite, HandleInheritability.None))
            using (var accessor = mmf.CreateViewAccessor(0, 1024 * 32, MemoryMappedFileAccess.ReadWrite))
            {
                var databaseAccessor = new MemoryMappedDatabaseAccessor(accessor);

                //if (isNew)
                //    databaseAccessor.InitializeDatabase();

                var existingRecords = databaseAccessor.ResolveRecords(this.Identifier);

                var encodedEndpoint = endpoint.Encode();
                var matchedExistingRecord = existingRecords.SingleOrDefault(record => ((System.Collections.IStructuralEquatable)record.Payload).Equals(encodedEndpoint, System.Collections.StructuralComparisons.StructuralEqualityComparer));
                if (matchedExistingRecord == null)
                {
                    databaseAccessor.AppendRecord(this.Identifier, encodedEndpoint);
                }
                else if (DateTime.UtcNow.ToUnixTimestamp() - matchedExistingRecord.Timestamp > 60)
                {
                    databaseAccessor.UpdateRecord(matchedExistingRecord.Offset, this.Identifier, encodedEndpoint);
                }
            }

            return Task.FromResult(true);
        }

        //public void Initialize()
        //{
        //    //            using (System.IO.File.Open(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite)) { }

        //    //            using (var fs = new FileStream(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        //    //#if NETSTANDARD
        //    //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
        //    //#else
        //    //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, null, HandleInheritability.None, false))
        //    //#endif
        //    using (var mmf = MemoryMappedFile.OpenExisting("Local\\" + this.MapName, MemoryMappedFileRights.ReadWrite, HandleInheritability.None))
        //    using (var accessor = mmf.CreateViewAccessor(0, 1024 * 32, MemoryMappedFileAccess.ReadWrite))
        //    {
        //        var databaseAccessor = new MemoryMappedDatabaseAccessor(accessor);

        //        databaseAccessor.InitializeDatabase();
        //    }
        //}
    }

    public interface IMemoryMappedDiscoverer<TEndpoint> : IDiscoverer<TEndpoint> where TEndpoint : IEncodableEndpoint
    {
        string MapName { get; }
    }

    public class MemoryMappedDiscoverer<TEndpoint> : ADiscoverer<TEndpoint>, IMemoryMappedDiscoverer<TEndpoint> where TEndpoint : IEncodableEndpoint
    {
        private string mapName;
        public string MapName
        {
            get
            {
                return this.mapName;
            }
        }

        private List<byte[]> blacklistedPayloads;
        public List<byte[]> BlacklistedPayloads => blacklistedPayloads;

        public MemoryMappedDiscoverer(string identifier, IEndpointDecoder<TEndpoint> endpointDecoder, string mapName)
            : base(identifier, endpointDecoder)
        {
            this.mapName = mapName;

            this.blacklistedPayloads = new List<byte[]>();
        }

        public async override Task<TEndpoint> Discover(int timeout = 0)
        {
            //            using (System.IO.File.Open(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite)) { }

            //            using (var fs = new FileStream(@"C:\Development\Projects\CFS\Wellspring\clr\test.dat", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            //#if NETSTANDARD
            //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
            //#else
            //            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(fs, this.MapName, 1024 * 32, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false))
            //#endif
            using (var mmf = MemoryMappedFile.OpenExisting(this.MapName, MemoryMappedFileRights.ReadWrite, HandleInheritability.None))
            using (var accessor = mmf.CreateViewAccessor(0, 1024 * 32, MemoryMappedFileAccess.ReadWrite))
            {
                var databaseAccessor = new MemoryMappedDatabaseAccessor(accessor);

                var startTime = DateTime.UtcNow;
                var records = this.ResolveValidRecords(databaseAccessor, this.Identifier).ToArray();
                while (records.Length <= 0)
                {
                    if (timeout > 0 && (DateTime.UtcNow - startTime).TotalMilliseconds > timeout)
                        throw new Exception("Discovery timeout");

                    await Task.Delay(500);

                    records = this.ResolveValidRecords(databaseAccessor, this.Identifier).ToArray();
                }

                var rand = new Random();
                var encodedPayload = records[rand.Next(0, records.Length)];

                return this.EndpointDecoder.Decode(encodedPayload.Payload);
            }
        }
        public override async Task<TEndpoint[]> DiscoverAll(int timeout = 0)
        {
            using (var mmf = MemoryMappedFile.OpenExisting(this.MapName, MemoryMappedFileRights.ReadWrite, HandleInheritability.None))
            using (var accessor = mmf.CreateViewAccessor(0, 1024 * 32, MemoryMappedFileAccess.ReadWrite))
            {
                var databaseAccessor = new MemoryMappedDatabaseAccessor(accessor);

                var startTime = DateTime.UtcNow;
                var records = this.ResolveValidRecords(databaseAccessor, this.Identifier).ToArray();
                while (records.Length <= 0)
                {
                    if (timeout > 0 && (DateTime.UtcNow - startTime).TotalMilliseconds > timeout)
                        throw new Exception("Discovery timeout");

                    await Task.Delay(500);

                    records = this.ResolveValidRecords(databaseAccessor, this.Identifier).ToArray();
                }

                return records.Select(encodedPayload => this.EndpointDecoder.Decode(encodedPayload.Payload)).ToArray();
            }
        }

        public override Task Blacklist(TEndpoint endpoint)
        {
            var encodedEndpoint = endpoint.Encode();
            this.BlacklistedPayloads.Add(encodedEndpoint);
            //var matchedExistingRecord = existingRecords.SingleOrDefault(record => ((System.Collections.IStructuralEquatable)record.Payload).Equals(encodedEndpoint, System.Collections.StructuralComparisons.StructuralEqualityComparer));

            return Task.FromResult(true);
        }

        private IEnumerable<MemoryMappedDatabaseRecord> ResolveValidRecords(MemoryMappedDatabaseAccessor databaseAccessor, string identifier)
        {
            return databaseAccessor.ResolveRecords(this.Identifier)
                .Where(r => DateTime.UtcNow.ToUnixTimestamp() - r.Timestamp <= 60)
                .Where(r => !this.BlacklistedPayloads.Any(br => ((System.Collections.IStructuralEquatable)r.Payload).Equals(br, System.Collections.StructuralComparisons.StructuralEqualityComparer)));
        }
    }
}