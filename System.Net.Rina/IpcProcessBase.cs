using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    /// <summary>
    /// This abstract class proivides common implementation for all IPC classes.
    /// </summary>
    public abstract class IpcProcessBase : IRinaIpc
    {
        public abstract Address LocalAddress { get; }
        public abstract Port AllocateFlow(FlowInformation flow);
        public abstract bool DataAvailable(Port port);
        public abstract Task<bool> DataAvailableAsync(Port port, CancellationToken ct);
        public abstract void DeallocateFlow(Port port);
        public abstract void DeregisterApplication(ApplicationNamingInfo appInfo);
        public abstract void Dispose();
        public abstract PortInformationOptions GetPortInformation(Port port);
        public abstract byte[] Receive(Port port);
        public abstract void RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler);
        public abstract int Send(Port port, byte[] buffer, int offset, int size);
        public abstract void SetBlocking(Port port, bool value);
    }
}
