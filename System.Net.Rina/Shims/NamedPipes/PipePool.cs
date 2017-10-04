using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina.Shims.NamedPipes
{

    /// <summary>
    /// Manages active pipes for the current context.
    /// </summary>
    internal class PipePool
    {
        /// <summary>
        /// A <see cref="Dictionary{TKey, TValue}"/> of <see cref="PipeClient"/> associated with their UNC name.
        /// </summary>
        private Dictionary<string, WeakReference<PipeClient>> m_pipeClients = new Dictionary<string, WeakReference<PipeClient>>();


        private readonly int pipeConnectTimeout = 10000;



        /// <summary>
        /// Creates a new <see cref="PipeClient"/> object for passed <paramref name="address"/>. Address object must be valid <see cref="Address.Uri"/> family.
        /// </summary>
        /// <remarks>
        /// The Uri can have different format:
        /// file://HOST/pipe/PIPENAME
        ///
        /// or
        ///
        /// net.pipe://HOST/PIPENAME
        /// </remarks>
        ///
        /// <param name="address"></param>
        /// <returns></returns>
        internal PipeClient GetOrCreatePipe(Address address, IPipeCallback callback)
        {
            if (address.Family != Address.Uri) throw new ArgumentException($"Address of type {nameof(Address.Uri)} expected.", nameof(address));

            var uri = address.Value as Uri;
            var uriUnc = uri.IsUnc ? uri : uri.AsPipeNameUnc();
            var host = uriUnc.Host;
            var pipe = Path.GetFileName(uriUnc.PathAndQuery);
            return GetOrCreatePipe(host, pipe, callback);
        }


        /// <summary>
        /// Gets existing or creates a new <see cref="PipeClient"/> object for the specified
        /// <paramref name="destinationAddress"/>.
        /// </summary>
        /// <returns>
        /// <see cref="PipeClient"/> object that should be connected to a pipe server with specified
        /// <paramref name="destinationAddress"/>.
        /// </returns>
        /// <remarks>
        /// This methods attempts to create and connect a new <see cref="PipeClient"/> object. For
        /// each remote endpoint there should be exactly one <see cref="PipeClient"/> object that is
        /// shared for all communication. While this method attempts to connect the <see
        /// cref="PipeClient"/> the caller should check that the pipe is really connected.
        /// </remarks>
        internal PipeClient GetOrCreatePipe(string serverName, string pipeName, IPipeCallback callback)
        {
            if (serverName == null) throw new ArgumentNullException(nameof(serverName));
            if (pipeName == null) throw new ArgumentNullException(nameof(pipeName));
            var addressKey = $@"{serverName}\{pipeName}";
            PipeClient pipeClient;
            WeakReference<PipeClient> pipeClientReference;
            if (!m_pipeClients.TryGetValue(addressKey, out pipeClientReference))
            {   // create a new object
                Trace.TraceInformation($"{nameof(GetOrCreatePipe)}: pipeClient associated with key {addressKey} cannot be found, I will create new pipeClient.");
                pipeClient = PipeClient.Create(serverName, pipeName, callback);
                pipeClientReference = new WeakReference<PipeClient>(pipeClient);
                m_pipeClients[addressKey] = pipeClientReference;
            }
            if (!pipeClientReference.TryGetTarget(out pipeClient))
            {   // recreate a new object
                Trace.TraceInformation($"{nameof(GetOrCreatePipe)}: pipeClient with key {addressKey} is dead, I will recreate it.");
                pipeClient = PipeClient.Create(serverName, pipeName, callback);
                pipeClientReference.SetTarget(pipeClient);
            }

            if (!pipeClient.IsConnected) pipeClient.Connect(pipeConnectTimeout);
            return pipeClient;
        }

        internal void ReleasePipe(PipeClient pipeClient)
        {
            Trace.TraceInformation($@"Releasing \\{pipeClient.ServerName}\{pipeClient.PipeName}.");
        }
    }
}
