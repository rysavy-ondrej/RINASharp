//
// DISCLAIMER: The underlying implementation of this ShimDIF is based on CSNamedPipes (https://github.com/webcoyote/CSNamedPipes) developed by Patrick Wyatt.
//
//
//
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina.Shims
{
    /// <summary>
    /// Specifies whether a connection is open or closed, connecting , or closing.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Connection is open.
        /// </summary>
        Open,

        /// <summary>
        /// Connection is tracked but it is closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Connection is being connected. The initialization is in progress.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connection is currently closing (it may be in graceful shutdown process).
        /// </summary>
        Closing,

        /// <summary>
        /// Connection is not being tracked by the context.
        /// </summary>
        Detached
    }

    // Interface for user code to receive notifications regarding pipe messages
    internal interface IPipeCallback
    {
        void OnAsyncConnect(PipeStream pipe, out Object state);

        void OnAsyncDisconnect(PipeStream pipe, Object state);

        void OnAsyncMessage(PipeStream pipe, Byte[] data, Int32 bytes, Object state);
    }

    // Internal data associated with pipes
    internal struct PipeData
    {
        public Byte[] data;
        public PipeStream pipe;
        public Object state;
    };

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
        /// A <see cref="Dictionary{TKey, TValue}"/> of <see cref="PipeClient"/> associated with their UNC name.
        /// </summary>
        private Dictionary<string, WeakReference<PipeClient>> _pipeClients = new Dictionary<string, WeakReference<PipeClient>>();

        /// <summary>
        /// <see cref="PipeServer"/> object that maintains all incoming Pipe connections.
        /// </summary>
        private PipeServer _pipeServer;

        private PipeIpcProcess(string localAddress)
        {
            var localhost = System.Net.Dns.GetHostEntry("").HostName;
            _localAddress = Address.PipeAddressUnc(localhost, localAddress);
            _pipeServer = new PipeServer(localAddress, this, 1);
        }

        public delegate void InvalidMessageReceivedEventHandler(object sender, Port port, PipeMessage message, MessageValidationResult info);

        public delegate void MessageDroppedEventHandler(object sender, Port port, PipeMessage message, PortError reason);

        public event InvalidMessageReceivedEventHandler InvalidMessageReceived;

        public event MessageDroppedEventHandler MessageDropped;

        [Flags]
        public enum MessageValidationResult : int { None = 0, DestinationCepId = 0x01, DestinationAddress = 0x02, SourceAddress = 0x04, MessageType = 0x08, NotRecognized = 0x10 }

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

        /// <summary>
        /// Gets the <see cref="ConnectionState"/> of the connection end point specified by the <paramref name="cepid"/>.
        /// </summary>
        /// <param name="cepid"></param>
        /// <returns>One of the connection state as defined by <see cref="ConnectionState"/>. </returns>
        internal ConnectionState GetConnectionState(UInt64 cepid)
        {
            if (_connectionsOpen.ContainsKey(cepid)) return ConnectionState.Open;
            if (_connectionsClosed.ContainsKey(cepid)) return ConnectionState.Closed;
            if (_connectionsConnecting.ContainsKey(cepid)) return ConnectionState.Connecting;
            if (_connectionsClosing.ContainsKey(cepid)) return ConnectionState.Closing;
            return ConnectionState.Detached;
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

        /// <summary>
        /// The wsaCleanup function terminates use of the IPC process.
        /// </summary>
        /// <returns></returns>
        protected override PortError wsaCleanup()
        {
            return PortError.Success;
        }

        /// <summary>
        /// This method establishes a connection to another application, exchanges connect data, and
        /// specifies required quality of service based on the specified QosParameters.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="ci"></param>
        /// <param name="callerData">Represents local CepId. </param>
        /// <param name="calleeData">Represents CepId of the remote application.</param>
        /// <param name="outflow"></param>
        /// <param name="inflow"></param>
        /// <param name="cep"></param>
        /// <returns></returns>
        protected override PortError wsaConnect(ConnectionEndpoint cep, ConnectionInformation ci, object callerData, out object calleeData, QosParameters outflow, QosParameters inflow)
        {
            var pcep = cep as PipeConnectionEndpoint;
            var sourceCepid = (ulong)callerData;
            var connectRequest = new PipeConnectRequest()
            {
                RequesterCepId = sourceCepid,
                DestinationAddress = ci.DestinationAddress,
                DestinationApplication = ci.DestinationApplication.ConnectionString,
                DestinationCepId = 0,
                SourceAddress = ci.SourceAddress,
                SourceApplication = ci.SourceApplication.ConnectionString
            };

            pcep.PipeClient = getOrCreatePipeFor(ci.DestinationAddress);

            // Message needs to be written at once, as we are working in message mode:
            PipeMessageEncoder.WriteMessage(connectRequest, pcep.PipeClient.Stream);
            // wait for response:
            var response = PipeMessageEncoder.ReadMessage(pcep.PipeClient.Stream) as PipeConnectResponse;
            if (response != null && response.Result == ConnectResult.Accept)
            {
                calleeData = response.ResponderCepId;
                return PortError.Success;
            }
            else
            {
                calleeData = 0;
                return PortError.ConnectionRefused;
            }
        }

        /// <summary>
        /// The function creates a connection endpoint that is bound to a specific provider.
        /// </summary>
        /// <returns></returns>
        protected override ConnectionEndpoint wsaConnection(Sockets.AddressFamily af, ConnectionType ct)
        {
            if (af != Address.Uri)
                throw new ArgumentException($"Address family is not supported by AddressFamily.NamedPipe.", nameof(af));

            var cep = new PipeConnectionEndpoint()
            {
                AddressFamily = af,
                ConnectionType = ct,
                LocalCepId = _cepidSpace.Next(),
            };
            return cep;
        }

        protected override PortError wsaGetIoctl(ConnectionEndpoint cep, WsaControlCode code, out object value)
        {
            switch (code)
            {
                case WsaControlCode.NonBlocking:
                    value = cep.Blocking;
                    return PortError.Success;

                case WsaControlCode.AvailableData:
                    value = dataAvailable(cep as PipeConnectionEndpoint);
                    return PortError.Success;
            }
            value = null;
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

            // if no data are in receive buffer then load some message:
            if (cep.ReceiveBuffer == null)
            {
                try
                {
                    if (cep.Blocking)
                    {
                        try
                        {
                            cep.ReceiveBuffer = cep.ReceiveQueue.Receive(cep.ReceiveTimeout);
                        }
                        catch (TimeoutException)
                        {
                            bytesReceived = 0;
                            return PortError.WouldBlock;
                        }
                    }
                    else
                    {
                        ArraySegment<byte> item;
                        if (cep.ReceiveQueue.TryReceive(out item))
                        {
                            cep.ReceiveBuffer = item;
                        }
                        else
                        {
                            bytesReceived = 0;
                            return PortError.WouldBlock;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    bytesReceived = -1;
                    return PortError.PortError;
                }
            }
            // Read bytes from the received buffer:
            if (size >= cep.ReceiveBuffer?.Count)
            {  // Consume all bytes
                bytesReceived = cep.ReceiveBuffer.Value.Count;
                Buffer.BlockCopy(cep.ReceiveBuffer.Value.Array, cep.ReceiveBuffer.Value.Offset, buffer, offset, bytesReceived);
                cep.ReceiveBuffer = null;
                return PortError.Success;
            }
            else
            {   // Copy only part of ReceiveBuffer, adjusting the rest
                bytesReceived = size;
                Buffer.BlockCopy(cep.ReceiveBuffer.Value.Array, cep.ReceiveBuffer.Value.Offset, buffer, offset, bytesReceived);
                cep.ReceiveBuffer = new ArraySegment<byte>(cep.ReceiveBuffer.Value.Array, cep.ReceiveBuffer.Value.Offset + bytesReceived, cep.ReceiveBuffer.Value.Count - bytesReceived);
                return PortError.Success;
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
                var bytesSent = PipeMessageEncoder.WriteMessage(msg, pcep.PipeClient.Stream);
                return PortError.Success;
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

        protected override PortError wsaSendDisconnect(ConnectionEndpoint cep)
        {
            var pcep = cep as PipeConnectionEndpoint;
            Debug.Assert(cep != null);
            var msg = new PipeDisconnectRequest()
            {
                SourceAddress = cep.Information.SourceAddress,
                DestinationAddress = cep.Information.DestinationAddress,
                DestinationCepId = cep.RemoteCepId,
                Flags = DisconnectFlags.Gracefull
            };

            PipeMessageEncoder.WriteMessage(msg, pcep.PipeClient.Stream);
            return PortError.Success;
        }

        protected override PortError wsaSetIoctl(ConnectionEndpoint cep, WsaControlCode code, object value)
        {
            var pcep = cep as PipeConnectionEndpoint;
            switch (code)
            {
                case WsaControlCode.NonBlocking:
                    cep.Blocking = (bool)value;
                    return PortError.Success;

                case WsaControlCode.Flush:
                    pcep.PipeClient.Stream.Flush();
                    return PortError.Success;
            }
            return PortError.InvalidArgument;
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
            return cep.ActualBufferSize;
        }

        /// <summary>
        /// Gets existing or creates a new <see cref="PipeClient"/> object for the specified <paramref name="destinationAddress"/>.
        /// </summary>
        /// <param name="destinationAddress"><see cref="Address"/> object of type <see cref="Address.Uri"/> that specifies remote pipe server address.</param>
        /// <returns><see cref="PipeClient"/> object that should be connected to a pipe server with specified <paramref name="destinationAddress"/>.</returns>
        /// <remarks>
        /// This methods attempts to create and connect a new <see cref="PipeClient"/> object. For each remote endpoint
        /// there should be exactly one <see cref="PipeClient"/> object that is shared for all communication.
        /// While this method attempts to connect the <see cref="PipeClient"/> the caller should check that the pipe is really connected.
        /// </remarks>
        private PipeClient getOrCreatePipeFor(Address destinationAddress)
        {
            var addressKey = (destinationAddress.Value as Uri)?.ToString();
            if (addressKey == null) throw new ArgumentException("Provided address is not of expected AddressFamily. Only Address.Uri family is accepted.", nameof(destinationAddress));
            PipeClient pipeClient;
            WeakReference<PipeClient> pipeClientReference;
            if (!_pipeClients.TryGetValue(addressKey, out pipeClientReference))
            {   // create a new object
                Trace.TraceInformation($"{nameof(getOrCreatePipeFor)}: pipeClient associated with key {addressKey} cannot be found, I will create new pipeClient.");
                pipeClient = PipeClient.Create(destinationAddress);
                pipeClientReference = new WeakReference<PipeClient>(pipeClient);
                _pipeClients[addressKey] = pipeClientReference;
            }
            if (!pipeClientReference.TryGetTarget(out pipeClient))
            {   // recreate a new object
                Trace.TraceInformation($"{nameof(getOrCreatePipeFor)}: pipeClient with key {addressKey} is dead, I will recreate it.");
                pipeClient = PipeClient.Create(destinationAddress);
                pipeClientReference.SetTarget(pipeClient);
            }

            if (!pipeClient.IsConnected) pipeClient.Connect(pipeConnectTimeout);
            return pipeClient;
        }

        private void releasePort(Port port)
        {
            lock (_portManagementSync)
            {
                _portsAllocated.Remove(port.Id);
                _portidSpace.Release(port.Id);
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

        // This code added to correctly implement the disposable pattern.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        #region Pipe Data and Control Methods

        private Helpers.UniqueRandomUInt32 _pipestreamidSpace = new Helpers.UniqueRandomUInt32();

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

                    default:
                        break;
                }
            }
            else
            {
                Trace.TraceError("Unknown or corrupted message received.");
            }
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
                if (reply == ConnectionRequestResult.Accept)
                {   // application accepts the connection, so we can create a new ConnectionEndpoint
                    // that manages the new flow.
                    // creates connection to remote endpoint:
                    var localCepId = _cepidSpace.Next();
                    var cep = new PipeConnectionEndpoint()
                    {
                        Blocking = true,
                        Information = new ConnectionInformation()
                        {   // we populate this table from flowInfo, remember that connection information
                            // is for local endpoint, while flowInfo is from the remote endpoint perspective.
                            // It means that we must switch source and destination:
                            SourceAddress = flowInfo.DestinationAddress,
                            SourceApplication = flowInfo.DestinationApplication,
                            DestinationAddress = flowInfo.SourceAddress,
                            DestinationApplication = flowInfo.SourceApplication
                        },
                        LocalCepId = localCepId,
                        PipeClient = getOrCreatePipeFor(flowInfo.SourceAddress),
                        Port = new Port(this, _portidSpace.Next()) { CepId = localCepId } ,
                        RemoteCepId = connectRequest.RequesterCepId,
                        Connected = true
                    };
                    _connectionsOpen.Add(cep.LocalCepId, cep);

                    Trace.TraceInformation($"Request accepted. New CEP created: {cep}");

                    // send accept response to remote end point. We use
                    // "control pipe" for this. However, further communication will
                    // be using "data pipe".
                    var connectAccept = new PipeConnectResponse()
                    {
                        Result = ConnectResult.Accept,
                        DestinationAddress = connectRequest.SourceAddress,
                        DestinationCepId = connectRequest.RequesterCepId,
                        SourceAddress = _localAddress,
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
                }
            }
            else // application not found, reject immediately
            {
                Trace.TraceWarning($"Application '{connectRequest.DestinationApplication}' not found in the current IPC. Reject will be send to the requester.");
                var connectReject = new PipeConnectResponse()
                {
                    Result = ConnectResult.Reject,
                    DestinationAddress = connectRequest.SourceAddress,
                    DestinationCepId = connectRequest.RequesterCepId,
                    SourceAddress = _localAddress,
                    RequesterCepId = connectRequest.RequesterCepId,
                    ResponderCepId = 0
                };
                PipeMessageEncoder.WriteMessage(connectReject, pipeStream);
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
            if (_connectionsOpen.TryGetValue(cepId, out cep))
            {
                var eflag = validateMessage(cep, message);
                if (eflag != MessageValidationResult.None)
                {
                    OnInvalidMessageReceived(cep.Port, message, eflag);
                    return;
                }
                if (cep.ActualBufferSize + message.Data.Count < cep.ReceiveBufferSize)
                {
                    cep.ReceiveQueue.Post(message.Data);
                }
                else
                {
                    OnMessageDropped(cep.Port, message, PortError.NoBufferSpaceAvailable);
                }
            }
        }

        #endregion Pipe Data and Control Methods

        #region Internal nested classes

        /// <summary>
        /// Describes the local endpoint of the connection between two IPCs.
        /// </summary>
        internal class PipeConnectionEndpoint : ConnectionEndpoint
        {
            public PipeClient PipeClient;

            public PipeConnectionEndpoint()
            {
                ReceiveBufferSize = 4096 * 4;
                ReceiveTimeout = TimeSpan.FromSeconds(30);
            }
        }

        #endregion Internal nested classes
    }

    internal class PipeClient
    {
        private readonly NamedPipeClientStream _pipe;

        public PipeClient(NamedPipeClientStream pipe)
        {
            this._pipe = pipe;
        }

        public bool IsConnected
        {
            get
            {
                return _pipe.IsConnected;
            }
        }

        public PipeStream Stream
        {
            get
            {
                return _pipe;
            }
        }

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
        public static PipeClient Create(Address address)
        {
            if (address.Family != Address.Uri) throw new ArgumentException($"Address of type {nameof(Address.Uri)} expected.", nameof(address));

            var uri = address.Value as Uri;
            var uriUnc = uri.IsUnc ? uri : uri.AsPipeNameUnc();
            var host = uriUnc.Host;
            var pipe = Path.GetFileName(uriUnc.PathAndQuery);
            return Create(host, pipe);
        }

        /// <summary>
        /// Creates a new <see cref="PipeClient"/> object for passed <paramref name="serverName"/> and <paramref name="pipename"/>.
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="pipename"></param>
        /// <returns></returns>
        public static PipeClient Create(String serverName, String pipename)
        {
            Trace.TraceInformation($"PipeClient.Create(serverName:{serverName},pipename:{pipename})");
            if (serverName == null) throw new ArgumentNullException(nameof(serverName));
            if (pipename == null) throw new ArgumentNullException(nameof(pipename));

            var pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            return new PipeClient(pipe);
        }

        public PipeStream Connect(Int32 timeout)
        {
            // NOTE: will throw on failure
            _pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            _pipe.ReadMode = PipeTransmissionMode.Message;

            return _pipe;
        }
    }

    internal class PipeServer
    {
        // TODO: parameterize so they can be passed by application
        public const Int32 SERVER_IN_BUFFER_SIZE = 4096;

        public const Int32 SERVER_OUT_BUFFER_SIZE = 4096;

        private readonly IPipeCallback m_callback;
        private readonly String m_pipename;
        private readonly PipeSecurity m_ps;

        private Dictionary<PipeStream, PipeData> m_pipes = new Dictionary<PipeStream, PipeData>();
        private bool m_running;

        public PipeServer(
            String pipename,
            IPipeCallback callback,
            int instances
        )
        {
            Trace.TraceInformation($"new PipeServer(pipename:{pipename},...,instances:{instances}");

            Debug.Assert(!m_running);
            m_running = true;

            // Save parameters for next new pipe
            m_pipename = pipename;
            m_callback = callback;

            // Provide full access to the current user so more pipe instances can be created
            m_ps = new PipeSecurity();
            m_ps.AddAccessRule(
                new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow)
            );
            m_ps.AddAccessRule(
                new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow
                )
            );

            // Start accepting connections
            for (int i = 0; i < instances; ++i)
                IpcServerPipeCreate();
        }

        public void IpcServerStop()
        {
            // Close all pipes asynchronously
            lock (m_pipes)
            {
                m_running = false;
                foreach (var pipe in m_pipes.Keys)
                    pipe.Close();
            }

            // Wait for all pipes to close
            for (;;)
            {
                int count;
                lock (m_pipes)
                {
                    count = m_pipes.Count;
                }
                if (count == 0)
                    break;
                Thread.Sleep(5);
            }
        }

        private void BeginRead(PipeData pd)
        {
            // Asynchronously read a request from the client
            bool isConnected = pd.pipe.IsConnected;
            if (isConnected)
            {
                try
                {
                    pd.pipe.BeginRead(pd.data, 0, pd.data.Length, OnAsyncMessage, pd);
                }
                catch (Exception)
                {
                    isConnected = false;
                }
            }

            if (!isConnected)
            {
                pd.pipe.Close();
                m_callback.OnAsyncDisconnect(pd.pipe, pd.state);
                lock (m_pipes)
                {
                    bool removed = m_pipes.Remove(pd.pipe);
                    Debug.Assert(removed);
                }
            }
        }

        private void IpcServerPipeCreate()
        {
            // Create message-mode pipe to simplify message transition
            // Assume all messages will be smaller than the pipe buffer sizes
            NamedPipeServerStream pipe = new NamedPipeServerStream(
                m_pipename,
                PipeDirection.InOut,
                -1,     // maximum instances
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                SERVER_IN_BUFFER_SIZE,
                SERVER_OUT_BUFFER_SIZE,
                m_ps
            );

            // Asynchronously accept a client connection
            pipe.BeginWaitForConnection(OnClientConnected, pipe);
        }

        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            PipeData pd = (PipeData)result.AsyncState;
            Int32 bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
                m_callback.OnAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

        private void OnClientConnected(IAsyncResult result)
        {
            // Complete the client connection
            NamedPipeServerStream pipe = (NamedPipeServerStream)result.AsyncState;
            pipe.EndWaitForConnection(result);

            // Create client pipe structure
            PipeData pd = new PipeData();
            pd.pipe = pipe;
            pd.state = null;
            pd.data = new Byte[SERVER_IN_BUFFER_SIZE];

            // Add connection to connection list
            bool running;
            lock (m_pipes)
            {
                running = m_running;
                if (running)
                    m_pipes.Add(pd.pipe, pd);
            }

            // If server is still running
            if (running)
            {
                // Prepare for next connection
                IpcServerPipeCreate();

                // Alert server that client connection exists
                m_callback.OnAsyncConnect(pipe, out pd.state);

                // Accept messages
                BeginRead(pd);
            }
            else
            {
                pipe.Close();
            }
        }
    }
}