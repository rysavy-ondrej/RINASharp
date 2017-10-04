using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Rina.Shims.NamedPipes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static System.Net.Rina.Shims.PipeIpcProcess;

namespace System.Net.Rina
{
    /// <summary>
    /// Defines control code for
    /// <see cref="IpcProcessBase.wsaSetIoctl(ConnectionEndpoint, IoctlControlCode, object)"/>
    /// and
    /// <see cref="IpcProcessBase.wsaGetIoctl(ConnectionEndpoint, IoctlControlCode, out object)"/>.
    /// </summary>
    public enum IoctlControlCode
    {
        /// <summary>
        /// Enable or disable non-blocking mode on connection end point (FIONBIO).
        /// </summary>
        NonBlocking = 0x01,

        /// <summary>
        /// Discards current contents of the sending queue associated with this connection end point (SIO_FLUSH).
        /// </summary>
        Flush = 0x02,

        /// <summary>
        /// Determines the amount of data that can be read atomically from connection end point. (FIONREAD)
        /// </summary>
        AvailableData = 0x03,

        /// <summary>
        /// Gets the <see cref="Task"/> that completes when data is available for read.
        /// </summary>
        AvailableDataTask = 0x04,
    }

    /// <summary>
    /// This abstract class provides common implementation for all IPC classes.
    /// </summary>
    public abstract class IpcProcessBase : IRinaIpc
    {
        /// <summary>
        /// Safe way to create random and unique CepId.
        /// </summary>
        private readonly CepIdSpace m_cepidSpace = new CepIdSpace();

        /// <summary>
        /// Collects connection end point in <see cref="ConnectionState.Closed"/> state.
        /// </summary>
        /// <remarks>
        /// Connections rest for a while in this map before they are removed. It is because some operations
        /// may be still in progress that would require an access to a connection information.
        /// </remarks>
        private readonly Dictionary<ulong, ConnectionEndpoint> m_connectionsClosed = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Collects connection end point in <see cref="ConnectionState.Closing"/> state.
        /// </summary>
        private readonly Dictionary<ulong, ConnectionEndpoint> m_connectionsClosing = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Collects connection end point in <see cref="ConnectionState.Connecting"/> state.
        /// </summary>
        private readonly Dictionary<ulong, ConnectionEndpoint> m_connectionsConnecting = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Collects connection end point in <see cref="ConnectionState.Open"/> state.
        /// This map represents a function: LocalCepid -> ConnectionEndpoint.
        /// </summary>
        private readonly Dictionary<ulong, ConnectionEndpoint> m_connectionsOpen = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Lock object used to synchronize the access to m_connections dictionaries.
        /// </summary>
        private readonly object m_connectionsLock = new object();

        /// <summary>
        /// The <see cref="Address"/> of the current <see cref="PipeIpcProcess"/> instance.
        /// </summary>
        protected Address m_localAddress;

        /// <summary>
        /// Helper that manages unique randomly generated port numbers.
        /// </summary>
        private readonly PortIdSpace m_portidSpace = new PortIdSpace();

        /// <summary>
        /// Tracks all created ports.
        /// </summary>
        private readonly Dictionary<ulong, Port> m_portsAllocated = new Dictionary<ulong, Port>();

        /// <summary>
        /// Used by implementation of <see cref="IDisposable"/> interface.
        /// </summary>
        private bool m_disposedValue = false;

        /// <summary>
        /// Specifies an amount of time the IPC waits for connection. Default value is 30s.
        /// </summary>
        private TimeSpan m_connectTimeout = new TimeSpan(0, 0, 30);

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        private Dictionary<Guid, RegisteredApplication> m_registeredApplications = new Dictionary<Guid, RegisteredApplication>();

        /// <summary>
        /// Lock object used to synchronize the access to <see cref="m_registeredApplications"/> dictionary.
        /// </summary>
        private object m_registeredApplicationsLock = new object();

        /// <summary>
        /// Maintains a collection of <see cref="Task"/> instances run by servers.
        /// </summary>
        private Dictionary<ulong, Task> m_serverTasks = new Dictionary<ulong, Task>();

        public delegate void InvalidMessageReceivedEventHandler(object sender, Port port, PipeMessage message, MessageValidationResult info);

        public delegate void MessageDroppedEventHandler(object sender, Port port, PipeMessage message, IpcError reason);

        public event InvalidMessageReceivedEventHandler InvalidMessageReceived;

        public event MessageDroppedEventHandler MessageDropped;

        [Flags]
        public enum MessageValidationResult : int { None = 0, DestinationCepId = 0x01, DestinationAddress = 0x02, SourceAddress = 0x04, MessageType = 0x08, NotRecognized = 0x10 }

        public Address LocalAddress { get { return m_localAddress; } }

        public void Abort(Port port)
        {
            var ct = new CancellationToken();
            var task = AbortAsync(port, true, ct);
            task.ConfigureAwait(false);
            task.Wait();
        }

        /// <summary>
        /// Aborts the specified connection.
        /// </summary>
        /// <remarks>
        /// The actions and events related to connection abort are as follows:
        /// 1. The client calls Abort that causes sending DisconnectRequest message of type Abort.
        /// 2. When the server receives DisconnectRequest message of type Abort, it should discard
        ///    all messages and may optionally send DisconnectRespose message
        /// 3. The client will not process any other messages except DisconnectResponse message.
        /// </remarks>
        /// <param name="cep"></param>
        /// <param name="waitForResponse"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<IpcError> AbortAsync(Port port, bool waitForResponse, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                var msg = new PipeDisconnectRequest()
                {
                    SourceAddress = cep.Information.SourceAddress,
                    DestinationAddress = cep.Information.DestinationAddress,
                    DestinationCepId = cep.RemoteCepId,
                    Flags = DisconnectFlags.Abort
                };
                await cep.SendMessageAsync(msg);

                moveCep(m_connectionsOpen, m_connectionsClosing, cep);

                if (waitForResponse)
                {
                    await WaitForControlMessage(cep, m => m.MessageType == PipeMessageType.DisconnectResponse, ct);
                }

                moveCep(m_connectionsClosing, m_connectionsClosed, cep);

                return IpcError.ConnectionAborted;
            }
            throw new IpcException(IpcError.NotConnected);
        }

        /// <summary>
        /// Removes connection end point from <paramref name="source"/> dictionary and adds it to <paramref name="target"/> dictionary.
        /// This method is thread safe as it uses <see cref="m_connectionsLock"/> to synchronize access to <paramref name="source"/> and <paramref name="target"/> dictionaries.
        /// </summary>
        /// <param name="source">The source <see cref="Dictionary{ulong, ConnectionEndpoint}"/>. This value can be null.</param>
        /// <param name="target">The target <see cref="Dictionary{ulong, ConnectionEndpoint}"/>.</param>
        /// <param name="cep"><see cref="ConnectionEndpoint"/> instance to remove from <paramref name="source"/> and add to <paramref name="target"/>.</param>
        /// <param name="moveOnlyIfFound">Moves only if the <see cref="ConnectionEndpoint"/> was found in the <paramref name="source"/>.</param>
        /// <returns> <see langword="true"/> if <see cref="ConnectionEndpoint"/> was added to <paramref name="target"/> dictionary. <see langword="false"/> otherwise.</returns>
        private bool moveCep(Dictionary<ulong, ConnectionEndpoint> source, Dictionary<ulong, ConnectionEndpoint> target, ConnectionEndpoint cep, bool moveOnlyIfFound = false)
        {
            Debug.Assert(cep != null);
            lock (m_connectionsLock)
            {
                var b = source?.Remove(cep.Id);
                if (b.HasValue?b.Value : false || !moveOnlyIfFound)
                {
                    target?.Add(cep.Id, cep);
                    return true;
                }
                return false;
            }
        }

        internal ConnectionState GetConnectionState(ConnectionEndpoint connectionEndpoint)
        {
            var cepid = connectionEndpoint.Id;
            if (m_connectionsOpen.ContainsKey(cepid)) return ConnectionState.Open;
            if (m_connectionsClosed.ContainsKey(cepid)) return ConnectionState.Closed;
            if (m_connectionsConnecting.ContainsKey(cepid)) return ConnectionState.Connecting;
            if (m_connectionsClosing.ContainsKey(cepid)) return ConnectionState.Closing;
            return ConnectionState.Detached;
        }

        /// <summary>
        /// Allocates the new flow according to the specified information.
        /// </summary>
        /// <remarks>
        /// <see cref="Connect(FlowInformation)"/> serves for two purposes known from <see cref="Sockets.Socket"/> programming:
        /// (i) it locally creates a new <see cref="Port"/> and (ii) it connects the newly created <see cref="Port"/>
        /// as specified in <see cref="FlowInformation"/>.
        /// </remarks>
        /// <returns><see cref="Port"/> object that describes the newly allocated flow.</returns>
        /// <param name="flowInfo">Flow information object.</param>
        public Port Connect(FlowInformation flowInfo)
        {
            var ct = new CancellationToken();
            var t = ConnectAsync(flowInfo, ct);
            t.ConfigureAwait(false);
            t.Wait(this.m_connectTimeout);
            return t.IsCompleted ? t.Result : null;
        }

        public async Task<Port> ConnectAsync(FlowInformation flowInfo, CancellationToken ct)
        {
            var ctype = GetConnectionType(flowInfo);
            var cepId = m_cepidSpace.Next();

            var cep = CepCreate(flowInfo.DestinationAddress.Family, ctype, cepId);

            moveCep(null, m_connectionsConnecting, cep);

            var ci = new ConnectionInformation()
            {
                SourceAddress = flowInfo.SourceAddress,
                SourceApplication = flowInfo.SourceApplication,
                DestinationAddress = flowInfo.DestinationAddress,
                DestinationApplication = flowInfo.DestinationApplication
            };

            var qos = selectQosParameters(flowInfo);

            var connectRequest = new PipeConnectRequest()
            {
                RequesterCepId = cep.LocalCepId,
                DestinationAddress = ci.DestinationAddress,
                DestinationApplication = ci.DestinationApplication.ConnectionString,
                DestinationCepId = 0,
                SourceAddress = ci.SourceAddress,
                SourceApplication = ci.SourceApplication.ConnectionString
            };

            // connect underlying connection, so we can use this connection to send control messages
            await CepOpenAsync(cep, ci, qos, qos);

            await cep.SendMessageAsync(connectRequest);

            var response = await WaitForControlMessage(cep, m => m.MessageType == PipeMessageType.ConnectResponse, ct) as PipeConnectResponse;

            if (response != null && response.Result == ConnectResult.Accepted)
            {
                moveCep(m_connectionsConnecting, m_connectionsOpen, cep);
                return bindConnectionPort(cep, ci, new CepIdType(response.ResponderCepId));
            }
            else
            {
                await CepCloseAsync(cep);
                moveCep(m_connectionsConnecting, m_connectionsClosed, cep);
                return null;
            }
        }

        public bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                object value;
                CepIoctl(cep, IoctlControlCode.AvailableData, null, out value);
                return (bool)value;
            }
            return false;
        }

        /// <summary>
        /// This task finishes when the data are available for the given port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns> If, when the task completes, its Result is true, more output is available in the source
        /// (though another consumer of the source may retrieve the data). If it returns false, more output
        /// is not and will never be available</returns>
        public Task<bool> DataAvailableAsync(Port port, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                object value;
                CepIoctl(cep, IoctlControlCode.AvailableDataTask, ct, out value);

                return value as Task<bool>;
            }
            throw new IpcException(IpcError.InvalidArgument);
        }

        /// <summary>
        /// Removes a registration of application described by <see cref="ApplicationInstanceHandle"/>.
        /// </summary>
        /// <param name="appInfo">An instance of <see cref="ApplicationInstanceHandle"/> that specifies the registered application.</param>
        /// <param name="option"></param>
        /// <param name="timeout"></param>
        public void DeregisterApplication(ApplicationInstanceHandle appInfo, DeregisterApplicationOption option, TimeSpan timeout)
        {
            if (option == DeregisterApplicationOption.DisconnectClients) throw new NotSupportedException($"Option {option} is not supported. Only {DeregisterApplicationOption.WaitForCompletition} is currently supported.");
            lock (m_registeredApplicationsLock)
            {
                m_registeredApplications.Remove(appInfo.Handle);
            }
        }

        /// <summary>
        /// Disconnects the specified <see cref="Port"/>.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <returns><see langword="true"/> if <see cref="Port"/> was disconnected. <see langword="false"/> if operation is still in progress.</returns>
        public bool Disconnect(Port port, TimeSpan timeout)
        {
            var ct = new CancellationToken();
            var t = DisconnectAsync(port, ct);
            t.ConfigureAwait(false);
            t.Wait(timeout);
            return t.IsCompleted ? t.Result : false;
        }

        /// <summary>
        /// This gracefully shutdown the connection.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="timeout">Specifies the time interval during which the process waits for DisconnectResponse.</param>
        /// <exception cref="IpcException">if connection associated with the presented port is not open.</exception>
        /// <returns>true if operation completed or false if timeout.</returns>
        public async Task<bool> DisconnectAsync(Port port, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                // Send disconnect request:
                var msg = new PipeDisconnectRequest()
                {
                    SourceAddress = cep.Information.SourceAddress,
                    DestinationAddress = cep.Information.DestinationAddress,
                    DestinationCepId = cep.RemoteCepId,
                    Flags = DisconnectFlags.Gracefull
                };

                var msgBytes = PipeMessageEncoder.WriteMessage(msg);
                await cep.SendQueue.SendAsync(new ArraySegment<byte>(msgBytes));
                cep.SendQueue.Complete();

                // Wait for disconnect response:
                PipeMessage response = await WaitForControlMessage(cep, m => m.MessageType == PipeMessageType.DisconnectResponse, ct);
                return (response as PipeDisconnectResponse)?.Flags == DisconnectFlags.Close;
            }
            else if (m_connectionsClosing.TryGetValue(port.CepId, out cep))
            {
                throw new IpcException(IpcError.AlreadyInProgress);
            }
            else
                throw new IpcException(IpcError.ConnectionNotFound);
        }

        /// <summary>
        /// To correctly implement the disposable pattern.
        /// Do not change this code. Put cleanup code in Dispose(bool disposing).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Gets the information about the specified <see cref="Port"/>.
        /// </summary>
        /// <param name="port">A <see cref="Port"/> instance to get information about.</param>
        /// <returns><see cref="PortInformationOptions"/> describing the specified <paramref name="port"/>.</returns>
        /// <exception cref="IpcException">if specified <paramref name="port"/> is not connected in the current IPC.</exception>
        public PortInformationOptions GetPortInformation(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                PortInformationOptions pi = 0;
                pi |= !cep.Blocking ? PortInformationOptions.NonBlocking : 0;
                pi |= cep.Connected ? PortInformationOptions.Connected : 0;
                return pi;
            }
            throw new IpcException(IpcError.NotPort);
        }

        /// <summary>
        /// Reads data from the given <see cref="Port"/>.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="socketFlags"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        public int Receive(Port port, byte[] buffer, int offset, int size, PortFlags flags)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep) || m_connectionsClosing.TryGetValue(port.CepId, out cep))
            {
                if (cep.Blocking)
                {
                    var ct = new CancellationToken();
                    var recvTask = cep.ReceiveQueue.ReadAsync(buffer, offset, size, ct);
                    recvTask.ConfigureAwait(false);
                    recvTask.Wait(cep.ReceiveTimeout);
                    return recvTask.IsCompleted ? recvTask.Result : 0;
                }
                else
                {
                    return cep.ReceiveQueue.TryRead(buffer, offset, size);
                }
            }
            else
                throw new IpcException(IpcError.InvalidArgument);
        }

        public async Task<int> ReceiveAsync(Port port, byte[] buffer, int offset, int size, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep) || m_connectionsClosing.TryGetValue(port.CepId, out cep))
            {
                var result = await cep.ReceiveQueue.ReadAsync(buffer, offset, size, ct);
                return result;
            }
            else
            {
                throw new IpcException(IpcError.InvalidArgument);
            }
        }

        /// <summary>
        /// Register application in the current <see cref="IpcProcessBase"/>.
        /// </summary>
        /// <param name="appInfo"></param>
        /// <param name="reqHandler"></param>
        /// <returns></returns>
        public ApplicationInstanceHandle RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            lock (m_registeredApplicationsLock)
            {
                Trace.TraceInformation($"Application '{appInfo.ApplicationName}' registered at process {this.LocalAddress}.");

                var appHandle = new ApplicationInstanceHandle();
                this.m_registeredApplications.Add(appHandle.Handle, new RegisteredApplication() { Handle = appHandle, ApplicationInfo = appInfo, RequestHandler = reqHandler });
                return appHandle;
            }
        }

        /// <summary>
        /// Release the specified <see cref="Port"/>. The port should be disconnected first.
        /// </summary>
        /// <param name="port">Port descriptor.</param>
        public void Release(Port port)
        {
            throw new NotImplementedException();
        }

        public int Send(Port port, byte[] buffer, int offset, int size)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                var sendTask = cep.SendQueue.Post(new ArraySegment<byte>(buffer, offset, size));
                return size;
            }
            else
                throw new IpcException(IpcError.NotConnected);
        }

        public async Task SendAsync(Port port, byte[] buffer, int offset, int size)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                await cep.SendQueue.SendAsync(new ArraySegment<byte>(buffer, offset, size));
            }
            else
            {
                throw new IpcException(IpcError.NotConnected);
            }
        }

        public void SetBlocking(Port port, bool value)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                cep.Blocking = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="ConnectionState"/> of the connection end point associated with the given port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns>One of the connection state as defined by <see cref="ConnectionState"/>. </returns>
        public ConnectionState GetPortState(Port port)
        {
            var cepid = port.CepId;
            if (m_connectionsOpen.ContainsKey(cepid)) return ConnectionState.Open;
            if (m_connectionsClosed.ContainsKey(cepid)) return ConnectionState.Closed;
            if (m_connectionsConnecting.ContainsKey(cepid)) return ConnectionState.Connecting;
            if (m_connectionsClosing.ContainsKey(cepid)) return ConnectionState.Closing;
            return ConnectionState.Detached;
        }

        /// <summary>
        /// The wsaCleanup function terminates the use of the IPC process. Override this function
        /// to implement proper handling of events related to shutting down the current IPC.
        /// </summary>
        /// <returns>A result of the operation as a <see cref="IpcError"/> value.</returns>
        protected abstract Task<IpcError> CepCleanupAsync();

        /// <summary>
        /// Called before the port bound to the specified <see cref="ConnectionEndpoint"/> is closed.
        /// </summary>
        /// <param name="cep"><see cref="ConnectionEndpoint"/> which should be closed.</param>
        /// <returns>A result of the operation as a <see cref="IpcError"/> value.</returns>
        protected abstract Task<IpcError> CepCloseAsync(ConnectionEndpoint cep);

        /// <summary>
        /// This method creates a new <see cref="ConnectionEndpoint"/>. Override this method to
        /// provide the object of the required type.
        /// </summary>
        /// <param name="af">Address family of the <see cref="ConnectionEndpoint"/>.</param>
        /// <param name="ct">Connection type of the <see cref="ConnectionEndpoint"/>.</param>
        /// <returns>
        /// The method may return null if either <see cref="Sockets.AddressFamily"/> nor
        /// <see cref="ConnectionType"/> is not supported.
        /// </returns>
        protected abstract ConnectionEndpoint CepCreate(Sockets.AddressFamily af, ConnectionType ct, CepIdType cepId);

        /// <summary>
        /// Perform necessary actions in order to disconnect the specified connection.
        /// </summary>
        /// <param name="cep"></param>
        /// <param name="abort"></param>
        /// <param name="ct"></param>
        /// <returns>A <see cref="Task"/> that when finished signalizes that the connection was closed or terminated.</returns>
        protected abstract Task<IpcError> CepDisconnectAsync(ConnectionEndpoint cep, CancellationToken ct);

        protected IpcError CepIoctl(ConnectionEndpoint cep, IoctlControlCode code, object inValue, out object outValue)
        {
            var pcep = cep;
            switch (code)
            {
                case IoctlControlCode.NonBlocking:
                    if (inValue != null)
                    {
                        outValue = null;
                        inValue = cep.Blocking;
                    }
                    else
                    {
                        outValue = cep.Blocking;
                    }
                    return IpcError.Success;

                case IoctlControlCode.Flush:
                    outValue = null;
                    return IpcError.Success;

                case IoctlControlCode.AvailableData:
                    outValue = dataAvailable(cep);
                    return IpcError.Success;

                case IoctlControlCode.AvailableDataTask:
                    var ct = (CancellationToken)inValue;
                    outValue = cep.ReceiveQueue.OutputAvailableAsync(ct);
                    return IpcError.Success;
            }
            outValue = null;
            return IpcError.InvalidArgument;
        }

        /// <summary>
        /// This method establishes a connection to another application, exchanges connect data, and
        /// specifies required quality of service based on the specified QosParameters.
        /// </summary>
        /// <param name="cep"><see cref="ConnectionEndpoint"/> used to create a new connection. This endpoint must not be already connected.</param>
        /// <param name="ci"><see cref="ConnectionInformation"/> object that specifies parameters of requested connection.</param>
        /// <param name="callerCepId">An object that represents the local CepId. </param>
        /// <param name="calleeCepId">An object that will be filled by the remote process with its CepId.</param>
        /// <param name="outflow">QoS specification for outgoing flow.</param>
        /// <param name="inflow">QoS specification for incoming flow.</param>
        /// <returns>A result of the operation as a <see cref="IpcError"/> value and <see cref="CepIdType"/> of the remote end point.</returns>
        protected abstract Task<IpcError> CepOpenAsync(ConnectionEndpoint cep, ConnectionInformation ci, QosParameters outflow, QosParameters inflow);

        protected abstract Task<IpcError> CepStartupAsync(ushort versionRequested, out object data);

        protected ConnectionRequestResult ConnectRequestAskApplication(FlowInformation flowInfo, RegisteredApplication app, out AcceptFlowHandler flowHandler)
        {
            flowHandler = null;
            ConnectionRequestResult requestReply;

            try
            {   // process application request handler in try-catch to avoid
                // system crash when application handler is incorrect.
                requestReply = app.RequestHandler(this, flowInfo, out flowHandler);
            }
            catch (Exception e)
            {
                Trace.TraceError($"Request handler method of {app.ApplicationInfo.ApplicationName}:{e.Message}");
                requestReply = ConnectionRequestResult.Reject;
            }

            Trace.TraceInformation($"Request handler of application '{flowInfo.DestinationApplication.ApplicationName}' replied {requestReply}.");
            return requestReply;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                m_disposedValue = true;
            }
        }

        protected RegisteredApplication FindApplication(string appName)
        {
            var appInfo = m_registeredApplications.Values.FirstOrDefault(r => { return r.ApplicationInfo.ApplicationName.Equals(appName, StringComparison.InvariantCultureIgnoreCase); });
            return appInfo;
        }

        protected virtual ConnectionType GetConnectionType(FlowInformation flowInfo)
        {
            return ConnectionType.Rdm;
        }

        /// <summary>
        /// Handles <see cref="PipeConnectRequest"/> message.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> object used to reply to <see cref="PipeConnectRequest"/>.</param>
        /// <param name="connectRequest">A <see cref="PipeConnectRequest"/> message.</param>
        protected PipeConnectResponse OnConnectRequest(PipeConnectRequest connectRequest)
        {
            var appInfo = new ApplicationNamingInfo(connectRequest.DestinationApplication);
            var app = FindApplication(appInfo.ApplicationName);

            Trace.TraceInformation($"Connection to application {appInfo} requested.");

            if (app != null)
            {
                var flowInfo = new FlowInformation()
                {
                    SourceAddress = connectRequest.SourceAddress,
                    SourceApplication = new ApplicationNamingInfo(connectRequest.SourceApplication),
                    DestinationAddress = connectRequest.DestinationAddress,
                    DestinationApplication = new ApplicationNamingInfo(connectRequest.DestinationApplication)
                };

                // create flow information from connect request message:
                AcceptFlowHandler flowHandler;
                var reply = ConnectRequestAskApplication(flowInfo, app, out flowHandler);
                switch (reply)
                {
                    case ConnectionRequestResult.Accept:
                        var t = acceptConnectionAsync(flowInfo, new CepIdType(connectRequest.RequesterCepId), flowHandler);
                        t.Wait();
                        return t.IsCompleted ? t.Result : null; 

                    case ConnectionRequestResult.Reject:
                    default:
                        {
                            Trace.TraceWarning($"Application '{connectRequest.DestinationApplication}' rejected connection. '{ConnectResult.Rejected}' will be send to the requester.");
                            var connectReject = new PipeConnectResponse()
                            {
                                Result = ConnectResult.Rejected,
                                DestinationAddress = connectRequest.SourceAddress,
                                DestinationCepId = connectRequest.RequesterCepId,
                                SourceAddress = m_localAddress,
                                RequesterCepId = connectRequest.RequesterCepId,
                                ResponderCepId = 0
                            };
                            return connectReject;
                        }
                }
            }
            else // application not found, reject immediately
            {
                Trace.TraceWarning($"Application '{connectRequest.DestinationApplication}' not found in the current IPC. '{ConnectResult.NotFound}' will be send to the requester.");

                var connectReject = new PipeConnectResponse()
                {
                    Result = ConnectResult.NotFound,
                    DestinationAddress = connectRequest.SourceAddress,
                    DestinationCepId = connectRequest.RequesterCepId,
                    SourceAddress = m_localAddress,
                    RequesterCepId = connectRequest.RequesterCepId,
                    ResponderCepId = 0
                };
                return connectReject;
            }
        }

        protected void OnControlMessage(PipeMessage message)
        {
            Debug.Assert(message != null);
            var cep = findCep(message.DestinationCepId);  
            if (cep != null && cep.State != ConnectionState.Closed)
            {
                cep.ControlMessageBuffer.Post(message);
            }                                                       
        }

        /// <summary>
        /// Receives data message and inserts data into a buffer of the target connection end point.
        /// </summary>
        /// <param name="cep">A non-null <see cref="ConnectionEndpoint"/> object.</param>
        /// <param name="message">A non-null <see cref="PipeDataMessage"/> instance.</param>
        protected void OnDataMessage(PipeDataMessage message)
        {
            Debug.Assert(message != null);
            var cep = findCep(message.DestinationCepId);
            if (cep != null && cep.Connected)
            {
                var eflag = validateMessage(cep, message);
                if (eflag != MessageValidationResult.None)
                {   // invalid message received, inform listener about this:
                    OnInvalidMessageReceived(cep.Port, message, eflag);
                    return;
                }

                if (cep.ReceiveQueue.Post(message.Data) == false)
                {   // message was not accepted, inform listener about this:
                    OnMessageDropped(cep.Port, message, IpcError.NoBufferSpaceAvailable);
                }
            }
            else
            {
                OnMessageDropped(cep.Port, message, IpcError.NotConnected);
            }            
        }

        /// <summary>
        /// Called by the derived class when disconnect request message was received.
        /// This method asserts that <see cref="ConnectionEndpoint"/> will transit to
        /// the corresponding state.
        /// </summary>
        /// <remarks>
        /// If disconnect request contains Abort flag then all buffered data
        /// in SEND queue will be removed. Otherwise, it is possible to send
        /// the remaining content of the buffer.
        /// The RECEIVE buffer will be closed for further messages but
        /// the application may read available bytes.
        /// </remarks>
        /// <param name="cep">A local <see cref="ConnectionEndpoint"/> instance.</param>
        /// <param name="abortAnnounced">It disconnect request is abortive.</param>
        /// <returns></returns>
        protected async Task<IpcError> OnDisconnectRequest(PipeDisconnectRequest request)
        {            
            Debug.Assert(request != null);

            var cep = findCep(request.DestinationCepId);
            if (cep?.State == ConnectionState.Open)
            {
                var abortRequested = request.Flags == DisconnectFlags.Abort;

                moveCep(m_connectionsOpen, m_connectionsClosing, cep);

                cep.Close(abortRequested);

                await cep.SendCompletion;

                await sendDisconnectResponseAsync(cep);

                return IpcError.Success;
            }
            else if (cep?.State == ConnectionState.Closing)
            {
                return IpcError.InProgress;
            }
            else
            {
                return IpcError.NotConnected;
            }
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
        protected virtual void OnMessageDropped(Port port, PipeMessage message, IpcError reason)
        {
            Trace.TraceWarning($"Port {port?.Id}: Message dropped. Reason: {reason}.");
            var handler = MessageDropped;
            if (handler != null)
            {
                MessageDropped(this, port, message, reason);
            }
        }

        /// <summary>
        /// Called on all messages received by the IPC.
        /// </summary>
        /// <param name="message">A new message received.</param>
        protected async Task<PipeMessage> OnMessageReceived(PipeMessage message)
        {
            Debug.Assert(message != null);


            switch (message.MessageType)
            {
                case PipeMessageType.Data:
                    OnDataMessage(message as PipeDataMessage);
                    break;

                case PipeMessageType.DisconnectRequest:
                    await OnDisconnectRequest(message as PipeDisconnectRequest);
                    break;

                case PipeMessageType.ConnectRequest:
                    return OnConnectRequest(message as PipeConnectRequest);

                case PipeMessageType.ConnectResponse:
                case PipeMessageType.DisconnectResponse:
                    OnControlMessage(message);
                    break;

                default:
                    Trace.TraceError($"{nameof(OnMessageReceived)}: message type {message.MessageType} not supported.");
                    break;
            }
            return null;
        }

        /// <summary>
        /// Waits till <see cref="ConnectionEndpoint.ControlMessageBuffer"/> buffer contains the message satisfying the specified <see cref="filter"/>.
        /// </summary>
        /// <param name="cep"><see cref="ConnectionEndpoint"/> instance storing the <see cref="ConnectionEndpoint.ControlMessageBuffer"/>.</param>
        /// <param name="filter"><see cref="Func{PipeMessage,bool}"/> function that represents a predicate to select the message.</param>
        /// <param name="ct"><see cref="CancellationToken"/> instance usable to cancel the task.</param>
        /// <returns></returns>
        protected async Task<PipeMessage> WaitForControlMessage(ConnectionEndpoint cep, Func<PipeMessage, bool> filter, CancellationToken ct)
        {
            var sink = new BufferBlock<PipeMessage>();
            cep.ControlMessageBuffer.LinkTo(sink, new DataflowLinkOptions { MaxMessages = 1 }, new Predicate<PipeMessage>(filter));
            var msg = await sink.ReceiveAsync();
            return msg;
        }

        /// <summary>
        /// Called when an application accepts the connection, so we can create a new <see cref="ConnectionEndpoint"/>
        /// that manages this new connection and runs the server <see cref="Task"/>.
        /// </summary>
        /// <param name="connectRequest"></param>
        /// <param name="flowInfo"></param>
        /// <param name="flowHandler"></param>
        /// <returns></returns>
        private async Task<PipeConnectResponse> acceptConnectionAsync(FlowInformation flowInfo, CepIdType requesterCepId, AcceptFlowHandler flowHandler)
        {
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

            var cep = CepCreate(flowInfo.DestinationAddress.Family, ConnectionType.Rdm, localCepId);

            QosParameters qos = new QosParameters();
            await CepOpenAsync(cep, ci, qos, qos);

            var port = bindConnectionPort(cep, ci, requesterCepId);

            // send accept response to remote end point. We use
            // "control pipe" for this. However, further communication will
            // be using "data pipe".
            var connectAccept = new PipeConnectResponse()
            {
                Result = ConnectResult.Accepted,
                DestinationAddress = cep.Information.DestinationAddress,
                DestinationCepId = cep.RemoteCepId,
                SourceAddress = m_localAddress,
                RequesterCepId = requesterCepId,
                ResponderCepId = cep.LocalCepId
            };
            // executes the handler on newly created Task
            // this task is running on its own...
            var serverTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    flowHandler(this, flowInfo, port);
                }
                catch (Exception e)
                {
                    Trace.TraceError($"A flow handler of application '{flowInfo.DestinationApplication.ApplicationName}' raised exception: {e.Message}");
                }
            });
            await serverTask.ConfigureAwait(false);
            m_serverTasks.Add(localCepId, serverTask);
            return connectAccept;
        }

        /// <summary>
        /// Creates fresh <see cref="Port"/> and binds it to the specified <see cref="ConnectionEndpoint"/>. Also it
        /// initializes the <see cref="ConnectionEndpoint"/> with information provided by <see cref="ConnectionInformation"/> and
        /// remote <see cref="CepIdType"/>.
        /// </summary>
        /// <param name="cep">A <see cref="ConnectionEndpoint"/> object that has not been bound so far.</param>
        /// <param name="ci"><see cref="ConnectionInformation"/> used to describe the connection.</param>
        /// <param name="remoteCepId">Remote CepId used to initialize the provided <see cref="ConnectionEndpoint"/>.</param>
        /// <returns></returns>
        private Port bindConnectionPort(ConnectionEndpoint cep, ConnectionInformation ci, CepIdType remoteCepId)
        {
            cep.Information = ci;
            cep.Open(remoteCepId);

            moveCep(null, m_connectionsOpen, cep);

            var port = new Port(this, m_portidSpace.Next())
            {
                CepId = cep.LocalCepId
            };
            cep.BindPort(port);
            m_portsAllocated.Add(port.Id, port);
            return port;
        }

        private int dataAvailable(ConnectionEndpoint cep)
        {
            return cep.ReceiveBufferSize - cep.ReceiveBufferSpace;
        }

        /// <summary>
        /// Finds <see cref="ConnectionEndpoint"/> specified by CepId. It looks in all connection dictionaries.
        /// </summary>
        /// <param name="cepId">Connection Endpoint Identifier.</param>
        /// <returns><see cref="ConnectionEndpoint"/> for the given CepId or null if no such connection is managed by this IPC.</returns>
        private ConnectionEndpoint findCep(ulong cepId)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(cepId, out cep)) return cep;
            if (m_connectionsConnecting.TryGetValue(cepId, out cep)) return cep;
            if (m_connectionsClosing.TryGetValue(cepId, out cep)) return cep;
            if (m_connectionsClosed.TryGetValue(cepId, out cep)) return cep;
            return null;
        }

        private ConnectionEndpoint getCepOrThrowException(Port port, IpcError error)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                return cep;
            }
            else
            {
                throw new IpcException(error);
            }
        }

        private QosParameters selectQosParameters(FlowInformation flowInfo)
        {
            return new QosParameters();
        }

        /// <summary>
        /// Asynchronously sends <see cref="PipeDisconnectResponse"/> with result <see cref="DisconnectFlags.Close"/>.
        /// </summary>
        /// <param name="cep"></param>
        /// <returns>Task that signalizes the completion of the operation.</returns>
        private async Task sendDisconnectResponseAsync(ConnectionEndpoint cep)
        {
            var pcep = cep as PipeConnectionEndpoint;
            var msg = new PipeDisconnectResponse()
            {
                SourceAddress = cep.Information.SourceAddress,
                DestinationAddress = cep.Information.DestinationAddress,
                DestinationCepId = cep.RemoteCepId,
                Flags = DisconnectFlags.Close
            };
            await cep.SendMessageAsync(msg);
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

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IpcProcessBase() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
    }

    /// <summary>
    /// Keeps information about registered applications.
    /// </summary>
    public class RegisteredApplication
    {
        /// <summary>
        /// Information about application, such as name, instance, etc.
        /// </summary>
        public ApplicationNamingInfo ApplicationInfo;

        /// <summary>
        /// Instance handle is unique id that is assigned to every registered application.
        /// </summary>
        public ApplicationInstanceHandle Handle;

        /// <summary>
        /// Request handler executed to check if the application accepts connection.
        /// </summary>
        public ConnectionRequestHandler RequestHandler;
    }

    public class WsaResult<T>
    {
        public WsaResult(IpcError result, T value)
        {
            Result = result;
            Value = value;
        }

        public IpcError Result { get; private set; }
        public T Value { get; private set; }
    }
}