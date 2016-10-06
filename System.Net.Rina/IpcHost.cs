using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    /// <summary>
    /// Provides a host for RINA Ipc Processes. 
    /// </summary>
    /// <remarks>
    /// The Ipc host is the runtime environment for hosting IPCP objects within a single OS process. 
    /// All <see cref="IpcProcess"/> instances should be assigned to some <see cref="IpcHost"/>. 
    /// </remarks>
    public class IpcHost: IDisposable
    {
        /// <summary>
        /// Creates IpcHost object. It also register IPC for all Shim DIFS.
        /// </summary>
        public IpcHost()
        {

        }


        /// <summary>
        /// Loads the IpcHost description from the configuration file and applies it to the runtime being constructed.
        /// </summary>
        public void LoadConfiguration()
        {

        }


        /// <summary>
        /// Registering a new Ipcp starts enrollment. 
        /// </summary>
        /// <param name="difAddress"></param>
        /// <param name="ipcp"></param>
        /// <param name="supportingIpcp"></param>
        public void RegisterIpcp(Address difAddress, IRinaIpc ipcp, IRinaIpc[] supportingIpcp)
        {
            
        }

        /// <summary>
        /// Enumerates all available IPC processes in the system. At the empty IpcHost, all IPCs for Shim DIFs are returned.
        /// </summary>
        /// <returns>An enumeration of IPC context defined in the system.</returns>
        public static IEnumerable<IRinaIpc> GetAvailableIpcProcesses()
        {
            return null;
        }

        /// <summary>
        /// Translates target address in the specified DIF to a reachability vector. It means to identification of
        /// supporting IPCP and the remote address in the supporting DIF where specified target address can be reach. 
        /// </summary>
        /// <param name="difAddress"></param>
        /// <param name="targetAddress"></param>
        /// <param name="localipcp"></param>
        /// <param name="remoteAddress"></param>
        /// <returns></returns>
        internal bool GetRemoteHostVector(Address difAddress, Address targetAddress, out IRinaIpc localipcp, out Address remoteAddress)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IpcHost() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
