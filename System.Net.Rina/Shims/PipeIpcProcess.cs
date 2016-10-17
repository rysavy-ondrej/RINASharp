//
// DISCLAIMER: The underlying implementation of this ShimDIF is based on CSNamedPipes (https://github.com/webcoyote/CSNamedPipes) developed by Patrick Wyatt.
//
//
//
using System;
using System.IO.Pipes;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using PacketDotNet.Utils;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina.Shims
{



    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs NamedPipes for communication.
    /// </summary>
    [ShimIpc("NamedPipe")]
    public class PipeIpcProcess : IpcProcessBase, IPipeCallback
    {

        /// <summary>
        /// Used for synchronization of operations on <see cref="PipeIpcProcess"/> object. Use in this pattern: lock(_generalLock) {  ...  }.
        /// </summary>
        object _generalLock = new object();


        /// <summary>
        /// <see cref="PipeServer"/> object that maintains all incoming Pipe connections.
        /// </summary>
        PipeServer _pipeServer;
        /// <summary>
        /// A <see cref="Dictionary{TKey, TValue}"/> of <see cref="PipeClient"/> associated with their UNC name.
        /// </summary>
        Dictionary<string, WeakReference<PipeClient>> _pipeClients = new Dictionary<string, WeakReference<PipeClient>>();

        /// <summary>
        /// The <see cref="Address"/> of the current <see cref="PipeIpcProcess"/> instance. 
        /// </summary>
        Address _localAddress;

        /// <summary>
        /// Creates a new <see cref="PipeIpcProcess"/> object using the provided <paramref name="localAddress"/> as the address.
        /// </summary>
        /// <param name="localAddress">A string representing local name of the process. It is used to assembly <see cref="Address"/> 
        /// that is represented by <see cref="Uri"/> of the form "\\localhost\localAddress". 
        /// </param>
        /// <returns>A new <see cref="PipeIpcProcess"/> object of the given address.</returns>
        /// <exception cref="ArgumentException">Thrown if the provide address is invalid or it already occupied.</exception>
        internal static IRinaIpc Create(string localAddress)
        {
            IRinaIpc instance = null;
            try
            {
                instance = new PipeIpcProcess(localAddress);
            }
            catch(Exception e)
            {
                throw new ArgumentException($"Cannot create IPC process for {localAddress}, {e.Message}.", nameof(localAddress));
            }
            if (instance == null) throw new ArgumentException($"Cannot create IPC process for {localAddress}.", nameof(localAddress));
            return instance;
        }

        /// <summary>
        /// Represents allocated flows/connections.
        /// This maps represents a function: [LocalCepId, RemoteCepId] -> ConnectionEndpoint. 
        /// </summary>
        MultiKeyDictionary<ulong, ulong, ConnectionEndpoint> _allocatedConnections = new MultiKeyDictionary<ulong, ulong, ConnectionEndpoint>();

        /// <summary>
        /// Represents deallocated flows/connections.
        /// This maps represents a function: [PortId, ConnectionId] -> ConnectionEndpoint. 
        /// </summary>
        /// <remarks>
        /// Connections rest for a while in this map before they are removed. It is because some operation 
        /// may be still in progress that would require an access to connection information.
        /// </remarks>
        MultiKeyDictionary<ulong, ulong, ConnectionEndpoint> _deallocatedConnections = new MultiKeyDictionary<ulong, ulong, ConnectionEndpoint>();

        private PipeIpcProcess(string localAddress)
        {
            _localAddress = Address.PipeAddressUnc("localhost", localAddress);
            _pipeServer = new PipeServer(localAddress, this, 1);            
        }

        public override Address LocalAddress
        {
            get
            {
                return _localAddress;
            }
        }


        object _portManagementLock = new object();
        Dictionary<ulong, Port> _allPorts = new Dictionary<ulong, Port>();
        Random _rand = new Random();
        private Port getFreshPort()
        {
            uint portId = 0;
            lock (_portManagementLock)
            {
                do
                {
                    portId = (uint)(((long)_rand.Next()) - (long)Int32.MinValue);
                } while (this._allPorts.ContainsKey(portId));
                var port = new Rina.Port(this, portId);
                _allPorts.Add(portId, port);
                return port;
            }            
        }
        private void releasePort(Port port)
        {
            lock(_portManagementLock)
            {
                _allPorts.Remove(port.Id);
            }
        }


        /// <summary>
        /// Allocates the new flow according to the specified information.
        /// </summary>
        /// <remarks>
        /// <see cref="AllocateFlow(FlowInformation)"/> serves for two purposes known from <see cref="Sockets.Socket"/> programming:
        /// (i) it locally creates a new <see cref="Port"/> and (ii) it connects the newly created <see cref="Port"/> 
        /// as specified in <see cref="FlowInformation"/>.
        /// </remarks>
        /// <returns><see cref="Port"/> object that describes the newly allocated flow.</returns>
        /// <param name="flowInfo">Flow information object.</param>
        public override Port AllocateFlow(FlowInformation flowInfo)
        {
            if (flowInfo.DestinationAddress.Family != Address.Uri)
                throw new ArgumentException($"Address family of provided DestinationAddress is different than AddressFamily.NamedPipe.", nameof(flowInfo));

            var ci = new ConnectionInformation()
            {
                SourceAddress = flowInfo.SourceAddress,
                SourceApplication = flowInfo.SourceApplication,
                DestinationAddress = flowInfo.DestinationAddress,
                DestinationApplication = flowInfo.DestinationApplication
            };

            var port = getFreshPort();
            var pipeClient = getOrCreatePipeClient(flowInfo.DestinationAddress);

            var remoteCepid = 0ul;
            var connectResult = connect(pipeClient, ci, port.Id, out remoteCepid);
            switch (connectResult)
            {
                case ConnectResult.Accept:
                    var cep = new ConnectionEndpoint()
                    {
                        Blocking = true,
                        Information = ci,
                        Port = port,
                        LocalCepId = port.Id,
                        RemoteCepId = remoteCepid,
                        PipeClient = pipeClient,
                        Connected = true,
                    };
                    _allocatedConnections.Add(port.Id, remoteCepid, cep);
                    return port;
                case ConnectResult.AuthenticationRequired:
                    throw new NotImplementedException();
                case ConnectResult.Reject:
                    Trace.TraceInformation($"Connection rejected by the remote process.");
                    releasePort(port);
                    return null;
                case ConnectResult.Fail:
                    Trace.TraceInformation($"Connection failed for an unknown reason.");
                    releasePort(port);
                    return null;
                default:
                    releasePort(port);
                    return null;
            }
        }

        const int pipeConnectTimeout =10000;
        private ConnectResult connect(PipeClient pipeClient, ConnectionInformation ci, ulong localCepid, out ulong remoteCepid)
        {
            return connect(pipeClient.Stream, ci.SourceAddress, ci.DestinationAddress, ci.SourceApplication.ProcessName, ci.DestinationApplication.ProcessName, localCepid, out remoteCepid);
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
        private PipeClient getOrCreatePipeClient(Address destinationAddress)
        {
            var addressKey = (destinationAddress.Value as Uri)?.ToString();
            if (addressKey == null) throw new ArgumentException("Provided address is not of expected AddressFamily. Only Address.Uri family is accepted.", nameof(destinationAddress));
            PipeClient pipeClient;
            WeakReference<PipeClient> pipeClientReference;
            if (!_pipeClients.TryGetValue(addressKey, out pipeClientReference))
            {   // create a new object
                Trace.TraceInformation($"{nameof(getOrCreatePipeClient)}: pipeClient associated with key {addressKey} cannot be found, I will create new pipeClient.");
                pipeClient = PipeClient.Create(destinationAddress);
                pipeClientReference = new WeakReference<PipeClient>(pipeClient);
                _pipeClients[addressKey] = pipeClientReference;
            }            
            if (!pipeClientReference.TryGetTarget(out pipeClient))
            {   // recreate a new object
                Trace.TraceInformation($"{nameof(getOrCreatePipeClient)}: pipeClient with key {addressKey} is dead, I will recreate it.");
                pipeClient = PipeClient.Create(destinationAddress);
                pipeClientReference.SetTarget(pipeClient);
            }

            if (!pipeClient.IsConnected) pipeClient.Connect(pipeConnectTimeout);
            return pipeClient;
        }

        /// <summary>
        /// Connects to other endpoint specified by address and application name. 
        /// </summary>
        /// <param name="pipeStream"></param>
        /// <param name="sourceAddress"></param>
        /// <param name="destinationAddress"></param>
        /// <param name="sourceApplication"></param>
        /// <param name="destinationApplication"></param>
        /// <param name="sourceCepid"></param>
        /// <returns>Returns CepId of the remote end point or 0 if the connection failed.</returns>
        ConnectResult connect(PipeStream pipeStream, Address sourceAddress, Address destinationAddress, string sourceApplication, string destinationApplication, ulong sourceCepid, out ulong remoteCepid)
        {
            var connectRequest = new PipeConnectRequest()
            {
                RequesterCepId = sourceCepid,
                DestinationAddress = destinationAddress,
                DestinationApplication = destinationApplication,
                DestinationCepId = 0,
                SourceAddress = sourceAddress,
                SourceApplication = sourceApplication
            };

            // Message needs to be written at once, as we are working in message mode:
            writeMessageToPipe(pipeStream, connectRequest);
            // wait for response:
            
            var response = readMessageFromPipe(pipeStream) as PipeConnectResponse;
            if (response == null || response?.Result == ConnectResult.Fail)
            {
                remoteCepid = 0;
                return ConnectResult.Fail;

            }
            else
            {
                remoteCepid = response.ResponderCepId;
                return response.Result;
            }
        }


        /// <summary>
        /// Writes <see cref="PipeMessage<"/> to <see cref="PipeStream"/>.
        /// It uses intermediate buffer represented by <see cref="MemoryStream"/> 
        /// because each calling of <see cref="PipeStream.Write(byte[], int, int)"/> 
        /// creates a new message.
        /// </summary>
        /// <param name="pipeStream">A <see cref="PipeStream"/> to which the message is written.</param>
        /// <param name="message">An object derived from <see cref="PipeMessage"/> representing the message.</param>
        /// <returns>The number of bytes written to the <see cref="PipeStream"/> object.</returns>
        static int writeMessageToPipe(PipeStream pipeStream, PipeMessage message)
        {
            using (var ms = new MemoryStream())
            {
                message.Serialize(ms);
                var buf = ms.GetBuffer();
                var count = (int)ms.Position;
                Trace.TraceInformation($"writeMessageToPipe: Message {count}B: {BitConverter.ToString(buf, 0, count)}");
                pipeStream.Write(buf, 0, count);
                return count;
            }
        }
        static PipeMessage readMessageFromPipe(PipeStream pipeStream)
        {
            var buffer = new byte[128];
            using (var ms = new MemoryStream())
            {
                do
                {
                    var len = pipeStream.Read(buffer, 0, buffer.Length);
                    if (len < 0) break;
                    ms.Write(buffer, 0, len);
                }
                while (!pipeStream.IsMessageComplete);
                var count = (int)ms.Position;
                ms.Position = 0;
                Trace.TraceInformation($"readMessageFromPipe: Message {count}B: {BitConverter.ToString(ms.GetBuffer(), 0, count)}");
                try
                {
                    return PipeMessage.Deserialize(ms);
                }
                catch(Exception e)
                {
                    Trace.TraceError($"{nameof(readMessageFromPipe)}: Error when deserializing message: {e.Message}");
                    return null;
                }
            }
        }
        private PipeMessage readMessageFromBuffer(byte[] data, int bytes)
        {
            Trace.TraceInformation($"readMessageFromPipe: Message {bytes}B: {BitConverter.ToString(data, 0, bytes)}");
            using (var ms = new MemoryStream(data, 0, bytes))
            {
                try
                {
                    return PipeMessage.Deserialize(ms);
                }
                catch (Exception e)
                {
                    Trace.TraceError($"{nameof(readMessageFromPipe)}: Error when deserializing message: {e.Message}");
                    return null;
                }
            }
        }


        public override bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                if (cep.ReceiveBuffer != null) return true;
                if (cep.ReceiveQueue.Count > 0) return true;
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
        public override Task<bool> DataAvailableAsync(Port port, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveQueue.OutputAvailableAsync(ct);
            }
            throw new PortException(PortError.InvalidArgument);
        }

        public override void DeallocateFlow(Port port)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the registration of the application specified by the <see cref="ApplicationNamingInfo"/>.
        /// </summary>
        /// <param name="appInfo"></param>
        public override void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            lock(_generalLock)
            {
                _registeredApplications.RemoveAt(_registeredApplications.FindIndex(x => x.ApplicationInfo.Equals(appInfo)));
            }
        }

        public override PortInformationOptions GetPortInformation(Port port)
        {
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                PortInformationOptions pi = 0;
                pi |= !cep.Blocking ? PortInformationOptions.NonBlocking : 0;
                pi |= cep.Connected ? PortInformationOptions.Connected : 0;
                return pi;
            }
            throw new PortException(PortError.InvalidArgument);
        }


        public delegate void InvalidMessageReceivedEventHandler(object sender, Port port, PipeMessage message, MessageValidationResult info);
        public event InvalidMessageReceivedEventHandler InvalidMessageReceived;

        protected virtual void OnInvalidMessageReceived(Port port, PipeMessage message, MessageValidationResult info)
        {
            Trace.TraceWarning($"Invalid message ({info}) received at port {port}, message: {message}.");
            var handler = InvalidMessageReceived;
            if (handler != null)
            {
                InvalidMessageReceived(this, port, message, info);
            }            
        }

        [Flags]
        public enum MessageValidationResult : int { None = 0, DestinationCepId = 0x01, DestinationAddress = 0x02, SourceAddress = 0x04, MessageType = 0x08, NotRecognized = 0x10 }

        MessageValidationResult validateMessage(ConnectionEndpoint cep, PipeMessage msg)
        {
            if (msg == null) return MessageValidationResult.NotRecognized;
            var merr = MessageValidationResult.None;
            merr |= (!Address.Equals(msg.SourceAddress,cep.Information.DestinationAddress)) ? MessageValidationResult.SourceAddress : 0;
            merr |= (!Address.Equals(msg.DestinationAddress,cep.Information.SourceAddress)) ? MessageValidationResult.DestinationAddress : 0;
            merr |= (msg.DestinationCepId != cep.LocalCepId) ? MessageValidationResult.DestinationCepId : 0;           
            return merr;
        }

        public delegate void MessageDroppedEventHandler(object sender, Port port, PipeMessage message, PortError reason);
        public event MessageDroppedEventHandler MessageDropped;

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
        /// Reads data from the given <see cref="Port"/>. 
        /// </summary>
        /// <param name="port"></param>
        /// <returns>A byte array containing received data. If operation would block than the result is zero length array. </returns>
        public override byte[] Receive(Port port)
        {
            var buffer = new byte[4096];
            PortError errorCode;
            var bytesRead = Receive(port, buffer, 0, buffer.Length, PortFlags.None, out errorCode);
            switch(errorCode)
            {
                case PortError.Success:
                    var result = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, result, 0, bytesRead);
                    return result;
                case PortError.WouldBlock:
                    return new byte[0];
                default:
                    throw new PortException(errorCode);
            }
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
        public int Receive(Port port, byte[] buffer, int offset, int size, PortFlags socketFlags, out PortError errorCode)
        {
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                if (!cep.PipeClient.IsConnected)
                {
                    errorCode = PortError.NotConnected;
                    return -1;
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
                                errorCode = PortError.WouldBlock;
                                return 0;
                            }
                        }
                        else
                        {
                            ByteArraySegment item;
                            if (cep.ReceiveQueue.TryReceive(out item))
                            {
                                cep.ReceiveBuffer = item;
                            }
                            else
                            {
                                errorCode = PortError.WouldBlock;
                                return 0;
                            }
                        }
                    }
                    catch(InvalidOperationException)
                    {
                        errorCode = PortError.PortError;
                        return -1;
                    }
                }
                // Read bytes from the received buffer:
                if (size >= cep.ReceiveBuffer.Length)
                {  // Consume all bytes
                    var consumedBytes = cep.ReceiveBuffer.Length;
                    Buffer.BlockCopy(cep.ReceiveBuffer.Bytes, cep.ReceiveBuffer.Offset, buffer, offset, consumedBytes);
                    cep.ReceiveBuffer = null;
                    errorCode = PortError.Success;
                    return consumedBytes;
                }
                else 
                {   // Copy only part of ReceiveBuffer, adjusting the rest
                    var consumedBytes = size;
                    Buffer.BlockCopy(cep.ReceiveBuffer.Bytes, cep.ReceiveBuffer.Offset, buffer, offset, consumedBytes);
                    cep.ReceiveBuffer = new ByteArraySegment(cep.ReceiveBuffer.Bytes, cep.ReceiveBuffer.Offset + consumedBytes, cep.ReceiveBuffer.Length - consumedBytes);
                    errorCode = PortError.Success;
                    return consumedBytes;
                }
            }
            else
                throw new PortException(PortError.InvalidArgument);
        }


        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        List<RegisteredApplication> _registeredApplications = new List<RegisteredApplication>();

        public override void RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            lock (_generalLock)
            {
                Trace.TraceInformation($"Application '{appInfo.ProcessName}' registered at process {this.LocalAddress}.");
                this._registeredApplications.Add(new RegisteredApplication() { ApplicationInfo = appInfo, RequestHandler = reqHandler });
            }
        }

        public override int Send(Port port, byte[] buffer, int offset, int size)
        {
            ConnectionEndpoint cep;
            if(_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                var msg = new PipeDataMessage()
                {
                    SourceAddress = cep.Information.SourceAddress,
                    DestinationAddress = cep.Information.DestinationAddress,
                    DestinationCepId = cep.RemoteCepId,
                    Data = new PacketDotNet.Utils.ByteArraySegment(buffer, offset, size)
                };
#if DEBUG 
                Trace.TraceInformation($"Send Message: {msg} using CEP: {cep}.");
#endif
                return writeMessageToPipe(cep.PipeClient.Stream, msg);
            }
            else
                return -1;
        }

        public override void SetBlocking(Port port, bool value)
        {
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                cep.Blocking = value;
            }                        
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
        // ~NamedPipeIpcProcess() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #region Pipe Data and Control Methods
        public void OnAsyncConnect(PipeStream pipe, out object state)
        {
            state = ++pipeId;
            Trace.TraceInformation($"OnAsyncConnect: new pipe connected, connection id={pipeId}");            
        }

        int pipeId = 0;

        public void OnAsyncDisconnect(PipeStream pipe, object state)
        {
            Trace.TraceInformation($"OnAsyncDisconnect: pipe id={state} disconnected.");
        }

        public void OnAsyncMessage(PipeStream pipeStream, byte[] data, int bytes, object state)
        {
            Trace.TraceInformation($"OnAsyncMessage: received message on pipe id={state}.");
            var msg = readMessageFromBuffer(data, bytes);
            Trace.TraceInformation($"OnAsyncMessage: read message {msg?.GetType().ToString()}.");
            if (msg != null)
            {
                switch (msg.MessageType)
                {
                    case PipeMessageType.ConnectRequest:
                        onConnectRequest(pipeStream, msg as PipeConnectRequest);
                        break;
                    case PipeMessageType.Data:
                        onDataReceived(pipeStream, msg as PipeDataMessage);
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
        /// Receives data message and inserts data into a buffer of the target connection end point.
        /// </summary>
        /// <param name="pipeStream"></param>
        /// <param name="message"></param>
        private void onDataReceived(PipeStream pipeStream, PipeDataMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var cepId = message.DestinationCepId;
            ConnectionEndpoint cep;
            if (_allocatedConnections.TryGetValue(primaryKey: cepId, val: out cep))
            {
                var eflag = validateMessage(cep, message);
                if (eflag != MessageValidationResult.None)
                {
                    OnInvalidMessageReceived(cep.Port, message, eflag);
                    return;
                }
                if (cep.ActualBufferSize + message.Data.Length < cep.ReceiveBufferSize)
                {
                    cep.ReceiveQueue.Post(message.Data);
                }
                else
                {                    
                    OnMessageDropped(cep.Port, message, PortError.NoBufferSpaceAvailable);
                }
            }
        }

        /// <summary>
        /// Handles <see cref="PipeConnectRequest"/> message.
        /// </summary>
        /// <param name="pipeStream"><see cref="PipeStream"/> object used to reply to <see cref="PipeConnectRequest"/>.</param>
        /// <param name="connectRequest">A <see cref="PipeConnectRequest"/> message.</param>
        private void onConnectRequest(PipeStream pipeStream, PipeConnectRequest connectRequest)
        {
            var app = _registeredApplications.Find(r => { return r.ApplicationInfo.ProcessName.Equals(connectRequest.DestinationApplication, StringComparison.InvariantCultureIgnoreCase); });
            if (app != null)
            {
                // create flow information from connect request message:
                var flowInfo = new FlowInformation()
                {
                    SourceAddress = connectRequest.SourceAddress,
                    SourceApplication = new ApplicationNamingInfo(connectRequest.SourceApplication,  "", "", ""),
                    DestinationAddress = connectRequest.DestinationAddress,
                    DestinationApplication = new ApplicationNamingInfo(connectRequest.DestinationApplication, "", "","")
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
                    Trace.TraceError($"RequestHandler method of {app.ApplicationInfo.ProcessName}:{e.Message}");
                }

                Trace.TraceInformation($"Request handler of application '{flowInfo.DestinationApplication.ProcessName}' replied {reply}.");
                if (reply == ConnectionRequestResult.Accept)
                {   // application accepts the connection, so we can create a new ConnectionEndpoint
                    // that manages the new flow.
                    var port = getFreshPort();
                    // creates connection to remote endpoint:
                    var pipeClient = getOrCreatePipeClient(flowInfo.SourceAddress);
                    var cep = new ConnectionEndpoint()
                    {
                        Blocking = true,
                        Information = new ConnectionInformation()
                        {   // we populate this table from flowInfo, remember that connection information 
                            // is for local endpoint, while flowinfo is from the remote endpoint perspective.
                            // It means that we must switch source and destination:
                            SourceAddress = flowInfo.DestinationAddress,
                            SourceApplication = flowInfo.DestinationApplication,
                            DestinationAddress = flowInfo.SourceAddress,
                            DestinationApplication = flowInfo.SourceApplication
                        },
                        LocalCepId = port.Id,
                        PipeClient = pipeClient,
                        Port = port,
                        RemoteCepId = connectRequest.RequesterCepId, 
                        Connected = true
                    };
                    _allocatedConnections.Add(port.Id, connectRequest.RequesterCepId, cep);

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
                        ResponderCepId = port.Id
                    };
                    writeMessageToPipe(pipeStream, connectAccept);
                    

                    // executes the handler on newly created Task
                    // this task is running on its own...                   
                    Task.Run(async () =>
                    {
                        try { await flowHandler(this, flowInfo, port); }
                        catch (Exception e)
                        {
                            Trace.TraceError($"A flow handler of application '{app.ApplicationInfo.ProcessName}' raised exception: {e.Message}");
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
                writeMessageToPipe(pipeStream, connectReject);
            }
        }
        #endregion

        #region Internal nested classes

        /// <summary>
        /// This is internal class for storing necessary information about each connection.
        /// </summary>
        internal class ConnectionInformation
        {
            internal ApplicationNamingInfo SourceApplication;
            internal ApplicationNamingInfo DestinationApplication;
            internal Address SourceAddress;
            internal Address DestinationAddress;
        }

        /// <summary>
        /// Describes the local endpoint of the connection between two IPCs.
        /// </summary>
        internal class ConnectionEndpoint
        {
            internal Port Port;
            /// <summary>
            /// Specifies whether the port is blocking or non-blocking.
            /// </summary>
            public bool Blocking { get; internal set; }
            /// <summary>
            /// Local ConnectionEndPoint Id is used to identify the connection.
            /// </summary>
            public ulong LocalCepId ;
            /// <summary>
            /// Remote ConnectionEndPoint Id is used to identify the connection.
            /// </summary>
            public ulong RemoteCepId;
            /// <summary>
            /// Provides information about the connection associated with the current ConnectionEndpoint.
            /// </summary>
            public ConnectionInformation Information;
            /// <summary>
            /// <see cref="PipeClient"/> object associated with this connection. Note that connections 
            /// share these clients.
            /// </summary>
            public PipeClient PipeClient;
            /// <summary>
            /// This is receive queue. New data are enqueued in this queue by the IPC process. Dequeue
            /// </summary>
            public readonly BufferBlock<ByteArraySegment> ReceiveQueue = new BufferBlock<ByteArraySegment>();
            /// <summary>
            /// This is byte buffer. Bytes are read from this buffer. 
            /// </summary>
            public ByteArraySegment ReceiveBuffer { get; internal set; }
            /// <summary>
            /// Actual size of the buffer. Represents total number of bytes currently in <see cref="ReceiveQueue"/>  and <see cref="ReceiveBuffer"/>.
            /// </summary>
            internal int ActualBufferSize;
            /// <summary>
            /// Sets or gets the maximum number of bytes that can be buffered.
            /// </summary>
            public int ReceiveBufferSize { set; get; }

            /// <summary>
            /// An amount of time for which the IPC process will wait until the completion of receive operation.
            /// </summary>
            public TimeSpan ReceiveTimeout { set; get; }
            
            /// <summary>
            /// Determines if the current endpoint is connected or disconnected.
            /// </summary>
            public bool Connected { get; internal set; }

            public ConnectionEndpoint()
            {
                ReceiveBufferSize = 4096*4;
                ReceiveTimeout = TimeSpan.FromSeconds(30);
            }
            public override string ToString()
            {
                return $"{Information.SourceApplication}@{Information.SourceAddress}:{LocalCepId} --> {Information.DestinationApplication}@{Information.DestinationAddress}:{RemoteCepId} [{Connected}]";
            }
        }

        /// <summary>
        /// Keeps information about registered applications.
        /// </summary>
        internal class RegisteredApplication
        {
            public ApplicationNamingInfo ApplicationInfo;
            public ConnectionRequestHandler RequestHandler;
        }

        #endregion
    }


    // Interface for user code to receive notifications regarding pipe messages
    interface IPipeCallback
    {
        void OnAsyncConnect(PipeStream pipe, out Object state);
        void OnAsyncDisconnect(PipeStream pipe, Object state);
        void OnAsyncMessage(PipeStream pipe, Byte[] data, Int32 bytes, Object state);
    }

    // Internal data associated with pipes
    struct PipeData
    {
        public PipeStream pipe;
        public Object state;
        public Byte[] data;
    };

    class PipeServer
    {
        // TODO: parameterize so they can be passed by application
        public const Int32 SERVER_IN_BUFFER_SIZE = 4096;
        public const Int32 SERVER_OUT_BUFFER_SIZE = 4096;

        private readonly String m_pipename;
        private readonly IPipeCallback m_callback;
        private readonly PipeSecurity m_ps;

        private bool m_running;
        private Dictionary<PipeStream, PipeData> m_pipes = new Dictionary<PipeStream, PipeData>();

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

        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            PipeData pd = (PipeData)result.AsyncState;
            Int32 bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
                m_callback.OnAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

    }


    class PipeClient
    {
        private readonly NamedPipeClientStream _pipe;

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

        public PipeClient(NamedPipeClientStream pipe)
        {
            this._pipe = pipe;
        }

        public bool IsConnected
        {
            get {
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

        public PipeStream Connect(Int32 timeout)
        {
            // NOTE: will throw on failure
            _pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            _pipe.ReadMode = PipeTransmissionMode.Message;

            return _pipe;
        }
    }

}