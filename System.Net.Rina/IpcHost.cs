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
    /// </remarks>
    public class IpcHost
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


        public void AddIddService()
        { }

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
    }
}
