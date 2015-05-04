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
    class IpcHost
    {
        /// <summary>
        /// Creates IpcHost object. It alos register IPC for all Shim DIFS.
        /// </summary>
        protected IpcHost()
        {

        }


        /// <summary>
        /// Loads the IpcHost description from the configuration file and applies it to the runtime being constructed.
        /// </summary>
        public void LoadConfiguration()
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

    }
}
