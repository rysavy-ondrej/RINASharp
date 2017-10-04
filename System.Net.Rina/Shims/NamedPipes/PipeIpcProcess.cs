//
// DISCLAIMER: The underlying implementation of this ShimDIF is based on CSNamedPipes (https://github.com/webcoyote/CSNamedPipes) developed by Patrick Wyatt.
//
//
//
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Rina.Shims.NamedPipes;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina.Shims
{
    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs NamedPipes for communication.
    /// </summary>
    /// <remarks>
    /// <see cref="PipeIpcProcess"/> implements IPC using NamedPipes.
    /// To simplify the implementation, for each connection two pipes are necessary. Each pipe is only one directional.
    /// Handling data of incoming pipe is done by implementing callback interface <see cref="IPipeCallback"/>.
    /// <see cref="IPipeCallback.OnPipeAsyncMessage(PipeStream, byte[], int, object)"/> method handles properly both data and control messages.
    /// Outgoing pipe is assigned to each connection and is directly used to send data.
    /// </remarks>
    [ShimIpc("NamedPipe")]
    public class PipeIpcProcess : IpcProcessBase, IPipeCallback
    {
        private const int pipeConnectTimeout = 10000;

        private readonly object _portManagementSync = new Object();

        /// <summary>
        /// Used for synchronization of operations on <see cref="PipeIpcProcess"/> object. Use in this pattern: lock(_generalLock) {  ...  }.
        /// </summary>
        private readonly object _syncRoot = new Object();

        /// <summary>
        /// <see cref="PipeServer"/> object that maintains all incoming Pipe connections.
        /// </summary>
        private PipeServer _pipeServer;

        private Helpers.UniqueRandomUInt32 _pipestreamidSpace = new Helpers.UniqueRandomUInt32();
        private PipePool m_pipePool = new PipePool();

        private PipeIpcProcess(string localAddress)
        {
            var localhost = System.Net.Dns.GetHostEntry("").HostName;
            m_localAddress = Address.PipeAddressUnc(localhost, localAddress);
            _pipeServer = new PipeServer(localAddress, this, 1);
        }

        /// <summary>
        /// Refers to <see cref="IpcHost"/>  that provides context for the current IpcProcess.
        /// </summary>
        public IpcHost Host { get; private set; }

        public void OnPipeAsyncConnect(PipeStream pipeStream, out object state)
        {
            state = _pipestreamidSpace.Next();
            Trace.TraceInformation($"OnAsyncConnect: new pipe connected, connection id={state}");
        }

        public void OnPipeAsyncDisconnect(PipeStream pipe, object state)
        {
            _pipestreamidSpace.Release((uint)state);
            Trace.TraceInformation($"OnAsyncDisconnect: pipe id={state} disconnected.");
        }

        public void OnPipeAsyncMessage(PipeStream pipeStream, byte[] data, int bytes, object state)
        {
            Trace.TraceInformation($"OnAsyncMessage: received message on pipe id={state}.");
            var msg = PipeMessageEncoder.ReadMessage(data, 0, bytes);
            Trace.TraceInformation($"OnAsyncMessage: read message {msg?.GetType().ToString()}.");
            if (msg != null)
            {
                var responseTask = OnMessageReceived(msg);
                responseTask.ContinueWith((t) =>
                {
                    if (t.IsCompleted)
                    {
                        PipeMessageEncoder.WriteMessage(responseTask.Result, pipeStream);
                    }
                });
            }
            else
            {
                Trace.TraceError("Unknown or corrupted message received.");
            }
        }

        /// <summary>
        /// Creates a new <see cref="PipeIpcProcess"/> object using the provided <paramref name="localAddress"/> as the address.
        /// </summary>
        /// <param name="localAddress">A string representing local name of the process. It is used to assembly <see cref="Address"/>
        /// that is represented by <see cref="Uri"/> of the form "\\localhost\localAddress".
        /// </param>
        /// <returns>A new <see cref="PipeIpcProcess"/> object of the given address.</returns>
        /// <exception cref="ArgumentException">Thrown if the provide address is invalid or it already occupied.</exception>
        internal static IRinaIpc Create(IpcHost host, string localAddress)
        {
            IRinaIpc instance = null;
            try
            {
                instance = new PipeIpcProcess(localAddress) { Host = host };
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Cannot create IPC process for {localAddress}, {e.Message}.", nameof(localAddress));
            }
            if (instance == null) throw new ArgumentException($"Cannot create IPC process for {localAddress}.", nameof(localAddress));
            return instance;
        }

        // This code added to correctly implement the disposable pattern.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        protected override Task<IpcError> CepCleanupAsync()
        {
            return Task.FromResult(IpcError.Success);
        }

        /// <summary>
        /// Removes data associated with passed ConnectionEndpoint. Here we do nothing as
        /// allocated buffers are removed by GC and we share PipeClients.
        /// </summary>
        /// <param name="cep"></param>
        /// <returns>It always returns <see cref="IpcError.Success"/>.</returns>
        protected override Task<IpcError> CepCloseAsync(ConnectionEndpoint cep)
        {
            Trace.TraceInformation($"Closing CEP: id={cep.Id}.");
            return Task.FromResult(IpcError.Success);
        }

        /// <summary>
        /// Connects the connection.
        /// </summary>
        /// <param name="cep"></param>
        /// <param name="ci"></param>
        /// <param name="outflow"></param>
        /// <param name="inflow"></param>
        /// <returns></returns>
        protected override Task<IpcError> CepOpenAsync(ConnectionEndpoint cep, ConnectionInformation ci, QosParameters outflow, QosParameters inflow)
        {
            var pcep = cep as PipeConnectionEndpoint;

            pcep.BindPipe(m_pipePool.GetOrCreatePipe(ci.DestinationAddress, this));

            return Task.FromResult(IpcError.Success);
        }

        protected override ConnectionEndpoint CepCreate(Sockets.AddressFamily af, ConnectionType ct, CepIdType id)
        {
            if (af != Address.Uri)
                throw new ArgumentException($"Address family is not supported by AddressFamily.NamedPipe.", nameof(af));

            var cep = new PipeConnectionEndpoint(af, ct, id);
            return cep;
        }

        /// <summary>
        /// The wsaStartup function initiates use of the current IPC process.
        /// </summary>
        /// <param name="versionRequested"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override Task<IpcError> CepStartupAsync(ushort versionRequested, out object data)
        {
            data = null;
            return Task.FromResult(IpcError.Success);
        }

        protected override Task<IpcError> CepDisconnectAsync(ConnectionEndpoint cep, CancellationToken ct)
        {
            var pcep = cep as PipeConnectionEndpoint;
            m_pipePool.ReleasePipe(pcep.PipeClient);
            return Task.FromResult(IpcError.Success);
        }

        /// <summary>
        /// Describes the local endpoint of the connection between two IPCs.
        /// </summary>
        /// <remarks>
        /// In addition to <see cref="ConnectionEndpoint"/> it contains <see cref="ControlMessageBuffer"/>
        /// buffer to aid processing of control messages and <see cref="PipeClient"/> used to send messages.
        /// </remarks>
        internal class PipeConnectionEndpoint : ConnectionEndpoint
        {
            /// <summary>
            /// Represents the Send manager. This object reads data from the Send Queue and
            /// send them to the outgoing pipe.
            /// </summary>
            private ActionBlock<PipeMessage> m_sendQueueManager;

            public PipeConnectionEndpoint(AddressFamily af, ConnectionType ct, CepIdType id) : base(af, ct, id)
            {
                m_sendQueueManager = new ActionBlock<PipeMessage>(sendNextMessageAsync);
                SendQueue.LinkTo(m_sendQueueManager, new DataflowLinkOptions() { PropagateCompletion = true });
            }

            /// <summary>
            /// Gets a <see cref="Task"/> that represents completion of the send block.
            /// </summary>
            public override Task SendCompletion
            {
                get
                {
                    return m_sendQueueManager.Completion;
                }
            }

            /// <summary>
            /// Stores associated <see cref="PipeClient"/> used to send messages to the remote CEP.
            /// </summary>
            internal PipeClient PipeClient { get; private set; }

            /// <summary>
            /// Sets <see cref="PipeClient"/> for the current <see cref="PipeConnectionEndpoint"/> .
            /// </summary>
            /// <param name="pipeClient"></param>
            public void BindPipe(PipeClient pipeClient)
            {
                this.PipeClient = pipeClient;
            }

            public override Task SendMessageAsync(PipeMessage message)
            {
                return PipeMessageEncoder.WriteMessageAsync(message, PipeClient.Stream);
            }

            protected override void OnAbort()
            {
                base.OnAbort();
                ((IDataflowBlock)m_sendQueueManager).Fault(new IpcException(IpcError.ConnectionAborted));
            }

            /// <summary>
            /// This method is an action of <see cref="m_sendQueueManager"/>.
            /// It sends messages from SendQueue.
            /// </summary>
            /// <param name="messageValue"></param>
            /// <returns></returns>
            private Task sendNextMessageAsync(PipeMessage message)
            {
                if (PipeClient?.IsConnected == true)
                {
                    return PipeMessageEncoder.WriteMessageAsync(message, PipeClient.Stream);
                }
                else
                    return Task.FromResult(false);
            }
        }
    }
}