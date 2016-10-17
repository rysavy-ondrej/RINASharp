using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    public enum IpcProcessType { Rina, WcfShim, PipeShim, IpShim, TcpShim, UdpShim, EtherShim };


    public static class IpcProcessFactory
    {

        public static IRinaIpc CreateProcess(IpcProcessType processType, string localAddress)
        {
            switch (processType)
            {
                case IpcProcessType.WcfShim:
                    return Shims.WcfIpcProcess.Create(localAddress);
                case IpcProcessType.PipeShim:
                    return Shims.PipeIpcProcess.Create(localAddress);
                default:
                    throw new NotImplementedException($"Could not create process for {processType}: Not implemented.");
            }
        }
    }
}
