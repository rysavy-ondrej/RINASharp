//
// DISCLAIMER: The underlying implementation of this ShimDIF is based on CSNamedPipes (https://github.com/webcoyote/CSNamedPipes) developed by Patrick Wyatt.
//
//
//
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Rina.Shims.NamedPipes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina.Shims
{
    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs NamedPipes for communication.
    /// </summary>
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

        public delegate void InvalidMessageReceivedEventHandler(object sender, Port port, PipeMessage message, MessageValidationResult info);

        public delegate void MessageDroppedEventHandler(object sender, Port port, PipeMessage message, PortError reason);

        public event InvalidMessageReceivedEventHandler InvalidMessageReceived;

        public event MessageDroppedEventHandler MessageDropped;

        [Flags]
        public enum MessageValidationResult : int { None = 0, DestinationCepId = 0x01, DestinationAddress = 0x02, SourceAddress = 0x04, MessageType = 0x08, NotRecognized = 0x10 }

        /// <summary>
        /// Refers to <see cref="IpcHost"/>  that provides context for the current IpcProcess.
        /// </summary>
        public IpcHost Host { get; private set; }

        public void OnAsyncConnect(PipeStream pipe, out object state)
        {
            state = _pipestreamidSpace.Next();
            Trace.TraceInformation($"OnAsyncConnect: new pipe connected, connection id={state}");
        }

        public void OnAsyncDisconnect(PipeStream pipe, object state)
        {
            _pipestreamidSpace.Release((uint)state);
            Trace.TraceInformation($"OnAsyncDisconnect: pipe id={state} disconnected.");
        }

        public void OnAsyncMessage(PipeStream pipeStream, byte[] data, int bytes, object state)
        {
            Trace.TraceInformation($"OnAsyncMessage: received message on pipe id={state}.");
            var msg = PipeMessageEncoder.ReadMessage(data, 0, bytes);
            Trace.TraceInformation($"OnAsyncMessage: read message {msg?.GetType().ToString()}.");
            if (msg != null)
            {
                switch (msg.MessageType)
                {
                    case PipeMessageType.ConnectRequest:
                        onAsyncMessage_ConnectRequest(pipeStream, msg as PipeConnectRequest);
                        break;

                    case PipeMessageType.Data:
                        onAsyncMessage_DataMessage(pipeStream, msg as PipeDataMessage);
                        break;

                    case PipeMessageType.DisconnectRequest:
                        onAsyncMessage_DisconnectRequest(pipeStream, msg as PipeDisconnectRequest);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Trace.TraceError("Unknown or corrupted message received.");
            }
        }

        private void onAsyncMessage_DisconnectRequest(PipeStream pipeStream, PipeDisconnectRequest message)
        {
            var cepId = message.DestinationCepId;
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(cepId, out cep))
            {
                OnDisconnectRequest(cep, message.Flags == DisconnectFlags.Abort).Wait();

                var msg = new PipeDisconnectResponse()
                {
                    SourceAddress = message.DestinationAddress,
                    DestinationAddress = message.SourceAddress,
                    DestinationCepId = cep.RemoteCepId,
                    Flags = DisconnectFlags.Close
                };
                PipeMessageEncoder.WriteMessage(msg, pipeStream);
            }
            else
            {
                Trace.TraceError($"{nameof(onAsyncMessage_DisconnectRequest)}: specified CepId={cepId} not found.");
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

        protected virtual void OnInvalidMessageReceived(Port port, PipeMessage message, MessageValidationResult info)
        {
            Trace.TraceWarning($"Invalid message ({info}) received at port {port}, message: {message}.");
            var handler = InvalidMessageReceived;
            if (handler != null)
            {
                InvalidMessageReceived(this, port, message, info);
            }
        }

        /// <summary>
        /// This callback is executed when the message is dropped because there is not enough space
        /// in the port buffer.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="message"></param>
        protected virtual void OnMessageDropped(Port port, PipeDataMessage message, PortError reason)
        {
            Trace.TraceWarning($"Port {port.Id}: Message dropped, not enough space in ReceiveBuffer.");
            var handler = MessageDropped;
            if (handler != null)
            {
                MessageDropped(this, port, message, reason);
            }
        }

        protected override PortError wsaCleanup()
        {
            return PortError.Success;
        }

        protected override PortError wsaConnect(ConnectionEndpoint cep, ConnectionInformation ci, CepIdType callerId, out CepIdType calleeId, QosParameters outflow, QosParameters inflow)
        {
            var pcep = cep as PipeConnectionEndpoint;
            var sourceCepid = (ulong)callerId;
            var connectRequest = new PipeConnectRequest()
            {
                RequesterCepId = sourceCepid,
                DestinationAddress = ci.DestinationAddress,
                DestinationApplication = ci.DestinationApplication.ConnectionString,
                DestinationCepId = 0,
                SourceAddress = ci.SourceAddress,
                SourceApplication = ci.SourceApplication.ConnectionString
            };

            pcep.PipeClient = m_pipePool.GetOrCreatePipe(ci.DestinationAddress);

            // Message needs to be written at once, as we are working in message mode:
            PipeMessageEncoder.WriteMessage(connectRequest, pcep.PipeClient.Stream);
            // wait for response:
            var response = PipeMessageEncoder.ReadMessage(pcep.PipeClient.Stream) as PipeConnectResponse;
            if (response != null && response.Result == ConnectResult.Accepted)
            {
                calleeId = new CepIdType(response.ResponderCepId);
                return PortError.Success;
            }
            else
            {
                calleeId = null;
                return PortError.ConnectionRefused;
            }
        }

        protected override ConnectionEndpoint wsaConnection(Sockets.AddressFamily af, ConnectionType ct)
        {
            if (af != Address.Uri)
                throw new ArgumentException($"Address family is not supported by AddressFamily.NamedPipe.", nameof(af));

            var cep = new PipeConnectionEndpoint()
            {
                AddressFamily = af,
                ConnectionType = ct,
                LocalCepId = m_cepidSpace.Next(),
            };
            return cep;
        }

        protected override PortError wsaIoctl(ConnectionEndpoint cep, WsaControlCode code, object inValue, out object outValue)
        {
            var pcep = cep as PipeConnectionEndpoint;
            switch (code)
            {
                case WsaControlCode.NonBlocking:
                    if (inValue != null)
                    {
                        outValue = null;
                        inValue = cep.Blocking;
                    }
                    else
                    {
                        outValue = cep.Blocking;
                    }
                    return PortError.Success;
                case WsaControlCode.Flush:
                    pcep.PipeClient.Stream.Flush();
                    outValue = null;
                    return PortError.Success;
                case WsaControlCode.AvailableData:
                    outValue = dataAvailable(cep as PipeConnectionEndpoint);
                    return PortError.Success;
                case WsaControlCode.AvailableDataTask:
                    var ct = (CancellationToken)inValue;
                    outValue = (cep as PipeConnectionEndpoint).ReceiveQueue.OutputAvailableAsync(ct);
                    return PortError.Success;
            }
            outValue = null;
            return PortError.InvalidArgument;
        }

        protected override PortError wsaRecv(ConnectionEndpoint cep, byte[] buffer, int offset, int size, out int bytesReceived, PortFlags socketFlags)
        {
            var pcep = cep as PipeConnectionEndpoint;
            if (!pcep.PipeClient.IsConnected)
            {
                bytesReceived = -1;
                return PortError.NotConnected;
            }
            if (cep.Blocking)
            {
                bytesReceived = pcep.ReceiveQueue.Read(buffer, offset, size, pcep.ReceiveTimeout);
                if (bytesReceived > 0)
                    return PortError.Success;
                else
                    return PortError.TimedOut;
            }
            else
            {
                bytesReceived = pcep.ReceiveQueue.TryRead(buffer, offset, size);
                if (bytesReceived > 0)
                    return PortError.Success;
                else
                    return PortError.WouldBlock;
            }
        }

        protected override PortError wsaSend(ConnectionEndpoint cep, byte[] buffer, int offset, int size)
        {
            var pcep = cep as PipeConnectionEndpoint;
            var msg = new PipeDataMessage()
            {
                SourceAddress = cep.Information.SourceAddress,
                DestinationAddress = cep.Information.DestinationAddress,
                DestinationCepId = cep.RemoteCepId,
                Data = new ArraySegment<byte>(buffer, offset, size)
            };
            Trace.TraceInformation($"Send Message: {msg} using CEP: {cep}.");

            try
            {
                var msgBytes = PipeMessageEncoder.WriteMessage(msg);
                var sent = pcep.SendQueue.Post(new ArraySegment<byte>(msgBytes));
                if (sent)
                    return PortError.Success;
                else
                    return PortError.NoBufferSpaceAvailable;
            }
            catch (IOException e)
            {
                Trace.TraceError($"wsaSend: {e.Message}.");
                return PortError.NotConnected;
            }
            catch (ObjectDisposedException e)
            {
                Trace.TraceError($"wsaSend: {e.Message}.");
                return PortError.OperationNotSupported;
            }
            catch (Exception e)
            {
                Trace.TraceError($"wsaSend: {e.Message}.");
                return PortError.Fault;
            }
        }
        protected override Task<PortError> wsaDisconnectAsync(ConnectionEndpoint cep, bool abort, CancellationToken ct)
        {
            var pcep = cep as PipeConnectionEndpoint;
            Debug.Assert(pcep != null);
            var msg = new PipeDisconnectRequest()
            {
                SourceAddress = cep.Information.SourceAddress,
                DestinationAddress = cep.Information.DestinationAddress,
                DestinationCepId = cep.RemoteCepId,
                Flags = abort ? DisconnectFlags.Abort : DisconnectFlags.Gracefull
            };
            PipeMessageEncoder.WriteMessage(msg, pcep.PipeClient.Stream);

            if (!abort)
            {
                return Task<PortError>.Run(() => {
                    var response = PipeMessageEncoder.ReadMessage(pcep.PipeClient.Stream) as PipeDisconnectResponse;
                    return response.Flags == DisconnectFlags.Close ? PortError.Success : PortError.ConnectionAborted;
                });                
            }
            return Task.FromResult(PortError.ConnectionAborted);
        }

        /// <summary>
        /// The wsaStartup function initiates use of the current IPC process.
        /// </summary>
        /// <param name="wVersionRequested"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override PortError wsaStartup(ushort versionRequested, out object data)
        {
            data = null;
            return PortError.Success;
        }

        private int dataAvailable(PipeConnectionEndpoint cep)
        {
            return cep.ReceiveBufferSize - cep.ReceiveBufferSpace;
        }

        /// <summary>
        /// Handles <see cref="PipeConnectRequest"/> message.
        /// </summary>
        /// <param name="pipeStream"><see cref="PipeStream"/> object used to reply to <see cref="PipeConnectRequest"/>.</param>
        /// <param name="connectRequest">A <see cref="PipeConnectRequest"/> message.</param>
        private void onAsyncMessage_ConnectRequest(PipeStream pipeStream, PipeConnectRequest connectRequest)
        {
            var appInfo = new ApplicationNamingInfo(connectRequest.DestinationApplication);
            Trace.TraceInformation($"Connection to application {appInfo} requested.");
            var app = FindApplication(appInfo.ApplicationName);

            if (app != null)
            {
                // create flow information from connect request message:
                onAsyncMessage_ConnectRequestAskApplication(pipeStream, connectRequest, app);
            }
            else // application not found, reject immediately
            {
                Trace.TraceWarning($"Application '{connectRequest.DestinationApplication}' not found in the current IPC. Reject will be send to the requester.");
                var connectReject = new PipeConnectResponse()
                {
                    Result = ConnectResult.NotFound,
                    DestinationAddress = connectRequest.SourceAddress,
                    DestinationCepId = connectRequest.RequesterCepId,
                    SourceAddress = m_localAddress,
                    RequesterCepId = connectRequest.RequesterCepId,
                    ResponderCepId = 0
                };
                PipeMessageEncoder.WriteMessage(connectReject, pipeStream);
            }
        }

        private void onAsyncMessage_ConnectRequestAskApplication(PipeStream pipeStream, PipeConnectRequest connectRequest, RegisteredApplication app)
        {
            var flowInfo = new FlowInformation()
            {
                SourceAddress = connectRequest.SourceAddress,
                SourceApplication = new ApplicationNamingInfo(connectRequest.SourceApplication),
                DestinationAddress = connectRequest.DestinationAddress,
                DestinationApplication = new ApplicationNamingInfo(connectRequest.DestinationApplication)
            };

            AcceptFlowHandler flowHandler = null;
            var reply = ConnectionRequestResult.Reject;

            try
            {   // process application request handler in try-catch to avoid
                // system crash when application handler is incorrect.
                reply = app.RequestHandler(this, flowInfo, out flowHandler);
            }
            catch (Exception e)
            {
                Trace.TraceError($"RequestHandler method of {app.ApplicationInfo.ApplicationName}:{e.Message}");
            }

            Trace.TraceInformation($"Request handler of application '{flowInfo.DestinationApplication.ApplicationName}' replied {reply}.");
            switch (reply)
            {
                case ConnectionRequestResult.Accept:
                    {   // application accepts the connection, so we can create a new ConnectionEndpoint
                        // that manages the new flow.
                        // creates connection to remote endpoint:

                        var ci = new ConnectionInformation()
                        {   // we populate this table from flowInfo, remember that connection information
                            // is for local endpoint, while flowInfo is from the remote endpoint perspective.
                            // It means that we must switch source and destination:
                            SourceAddress = flowInfo.DestinationAddress,
                            SourceApplication = flowInfo.DestinationApplication,
                            DestinationAddress = flowInfo.SourceAddress,
                            DestinationApplication = flowInfo.SourceApplication
                        };

                        var localCepId = m_cepidSpace.Next();
                        var cep = wsaConnection(flowInfo.DestinationAddress.Family, ConnectionType.Rdm) as PipeConnectionEndpoint;
                        cep.PipeClient = m_pipePool.GetOrCreatePipe(flowInfo.SourceAddress);
                        wsaBind(cep, ci, new CepIdType(connectRequest.RequesterCepId));

                        Trace.TraceInformation($"Request accepted. New CEP created: {cep}");

                        // send accept response to remote end point. We use
                        // "control pipe" for this. However, further communication will
                        // be using "data pipe".
                        var connectAccept = new PipeConnectResponse()
                        {
                            Result = ConnectResult.Accepted,
                            DestinationAddress = connectRequest.SourceAddress,
                            DestinationCepId = connectRequest.RequesterCepId,
                            SourceAddress = m_localAddress,
                            RequesterCepId = connectRequest.RequesterCepId,
                            ResponderCepId = cep.LocalCepId
                        };
                        PipeMessageEncoder.WriteMessage(connectAccept, pipeStream);

                        // executes the handler on newly created Task
                        // this task is running on its own...
                        Task.Run(async () =>
                        {
                            try { await flowHandler(this, flowInfo, cep.Port); }
                            catch (Exception e)
                            {
                                Trace.TraceError($"A flow handler of application '{app.ApplicationInfo.ApplicationName}' raised exception: {e.Message}");
                            }
                        }).ConfigureAwait(false);
                        break;
                    }

                case ConnectionRequestResult.Reject:
                    {
                        Trace.TraceWarning($"Application '{connectRequest.DestinationApplication}' rejected connection. Reject will be send to the requester.");
                        var connectReject = new PipeConnectResponse()
                        {
                            Result = ConnectResult.Rejected,
                            DestinationAddress = connectRequest.SourceAddress,
                            DestinationCepId = connectRequest.RequesterCepId,
                            SourceAddress = m_localAddress,
                            RequesterCepId = connectRequest.RequesterCepId,
                            ResponderCepId = 0
                        };
                        PipeMessageEncoder.WriteMessage(connectReject, pipeStream);
                        break;
                    }
            }
        }

        /// <summary>
        /// Receives data message and inserts data into a buffer of the target connection end point.
        /// </summary>
        /// <param name="pipeStream"></param>
        /// <param name="message"></param>
        private void onAsyncMessage_DataMessage(PipeStream pipeStream, PipeDataMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var cepId = message.DestinationCepId;
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(cepId, out cep))
            {
                var pcep = cep as PipeConnectionEndpoint;
                var eflag = validateMessage(cep, message);
                if (eflag != MessageValidationResult.None)
                {   // invalid message received, inform listener about this:
                    OnInvalidMessageReceived(cep.Port, message, eflag);
                    return;
                }

                if (pcep.ReceiveQueue.Post(message.Data) == false)
                {   // message was not accepted, inform listener about this:
                    OnMessageDropped(cep.Port, message, PortError.NoBufferSpaceAvailable);
                }
            }
        }

        /// <summary>
        /// Validates the message with respect to information from the given <see cref="ConnectionEndpoint"/>.
        /// </summary>
        /// <param name="cep"></param>
        /// <param name="msg"></param>
        /// <returns>
        /// <see cref="MessageValidationResult"/> value that contains the found issues. If this value
        /// is <see cref="MessageValidationResult.None"/> then the message is valid.
        /// </returns>
        private MessageValidationResult validateMessage(ConnectionEndpoint cep, PipeMessage msg)
        {
            if (msg == null) return MessageValidationResult.NotRecognized;
            var merr = MessageValidationResult.None;
            merr |= (!Address.Equals(msg.SourceAddress, cep.Information.DestinationAddress)) ? MessageValidationResult.SourceAddress : 0;
            merr |= (!Address.Equals(msg.DestinationAddress, cep.Information.SourceAddress)) ? MessageValidationResult.DestinationAddress : 0;
            merr |= (msg.DestinationCepId != cep.LocalCepId) ? MessageValidationResult.DestinationCepId : 0;
            return merr;
        }
        /// <summary>
        /// Removes data associated with passed ConnectionEndpoint. Here we do nothing as 
        /// allocated buffers are removed by GC and we share PipeClients. 
        /// </summary>
        /// <param name="cep"></param>
        /// <returns>It always returns <see cref="PortError.Success"/>.</returns>
        protected override PortError wsaClose(ConnectionEndpoint cep)
        {
            Trace.TraceInformation($"Closing CEP: id={cep.Id}.");                        
            return PortError.Success;
        }

        /// <summary>
        /// Describes the local endpoint of the connection between two IPCs.
        /// </summary>
        internal class PipeConnectionEndpoint : ConnectionEndpoint
        {
            public PipeClient PipeClient;

            public ITargetBlock<ArraySegment<byte>> SendQueueManager;



            Task sendMessage(ArraySegment<byte> messageValue)
            {
                if (PipeClient?.IsConnected == true)
                {
                    return PipeClient.Stream.WriteAsync(messageValue.Array, messageValue.Offset, messageValue.Count);
                }
                else
                    return Task.FromResult(false);
            }

            public PipeConnectionEndpoint()
            {
                ReceiveBufferSize = 4096 * 4;
                ReceiveTimeout = TimeSpan.FromSeconds(30);
                SendQueueManager = new ActionBlock<ArraySegment<byte>>(sendMessage);
                SendQueueManager.Completion.ContinueWith((t) => 
                {
                    SendCompletedEvent.Set();
                });
                SendQueue.LinkTo(SendQueueManager, new DataflowLinkOptions() { PropagateCompletion = true });
            }
            /// <summary>
            /// This is receive queue. New data are enqueued in this queue by the IPC process.
            /// </summary>
            public readonly ReceiveBufferBlock<byte> ReceiveQueue = new ReceiveBufferBlock<byte>();

            /// <summary>
            /// This is send queue that stores data before it is sent by the IPC process. 
            /// </summary>
            public readonly BufferBlock<ArraySegment<byte>> SendQueue = new BufferBlock<ArraySegment<byte>>();

            public override int ReceiveBufferSpace
            {
                get
                {
                    return ReceiveBufferSize - ReceiveQueue.ItemsAvailable;
                }
            }

            protected override void OnStateChanged(ConnectionState oldValue, ConnectionState newValue)
            {
                switch(newValue)
                {
                    case ConnectionState.Closing: SendQueue.Complete(); break;
                    case ConnectionState.Closed: SendQueueManager.Fault(new PortException(PortError.ConnectionAborted)); break;
                }
            }
        }
    }
}