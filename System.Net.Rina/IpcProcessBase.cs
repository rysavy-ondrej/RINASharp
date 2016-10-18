using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina
{
    public enum ConnectionType
    {
        /// <summary>
        /// Stream oriented connection.
        /// </summary>
        Stream = 1,

        /// <summary>
        /// Datagram (block) oriented connection type.
        /// </summary>
        Dgram = 2,

        /// <summary>
        /// Raw connection type.
        /// </summary>
        Raw = 3,

        /// <summary>
        /// Reliably delivered message oriented connection type.
        /// </summary>
        Rdm = 4,

        /// <summary>
        /// Unknown type.
        /// </summary>
        Unknown = -1,
    }

    /// <summary>
    /// Describes the local endpoint of the connection between two IPCs.
    /// </summary>
    public class ConnectionEndpoint
    {
        /// <summary>
        /// This is receive queue. New data are enqueued in this queue by the IPC process. Dequeue
        /// </summary>
        public readonly BufferBlock<ArraySegment<byte>> ReceiveQueue = new BufferBlock<ArraySegment<byte>>();

        /// <summary>
        /// Provides information about the connection associated with the current ConnectionEndpoint.
        /// </summary>
        public ConnectionInformation Information;

        /// <summary>
        /// Local ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public ulong LocalCepId;

        /// <summary>
        /// Remote ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public ulong RemoteCepId;

        /// <summary>
        /// Actual size of the buffer. Represents total number of bytes currently in <see cref="ReceiveQueue"/>  and <see cref="ReceiveBuffer"/>.
        /// </summary>
        internal int ActualBufferSize;

        internal Port Port;

        public ConnectionEndpoint()
        {
            ReceiveBufferSize = 4096 * 4;
            ReceiveTimeout = TimeSpan.FromSeconds(30);
            Blocking = true;
        }

        /// <summary>
        /// Represents an address family used with this connection end point.
        /// </summary>
        public Sockets.AddressFamily AddressFamily { get; internal set; }

        /// <summary>
        /// Specifies whether the port is blocking or non-blocking.
        /// </summary>
        public bool Blocking { get; internal set; }

        /// <summary>
        /// Determines if the current endpoint is connected or disconnected.
        /// </summary>
        public bool Connected { get; internal set; }

        /// <summary>
        /// Represents connection type of this connection end point.
        /// </summary>
        public ConnectionType ConnectionType { get; internal set; }

        /// <summary>
        /// This is byte buffer. Bytes are read from this buffer.
        /// </summary>
        public Nullable<ArraySegment<byte>> ReceiveBuffer { get; internal set; }

        /// <summary>
        /// Sets or gets the maximum number of bytes that can be buffered.
        /// </summary>
        public int ReceiveBufferSize { set; get; }

        /// <summary>
        /// An amount of time for which the IPC process will wait until the completion of receive operation.
        /// </summary>
        public TimeSpan ReceiveTimeout { set; get; }

        public override string ToString()
        {
            return $"{Information.SourceApplication}@{Information.SourceAddress}:{LocalCepId} --> {Information.DestinationApplication}@{Information.DestinationAddress}:{RemoteCepId} [{Connected}]";
        }
    }

    /// <summary>
    /// This class describes a single connection.
    /// </summary>
    public class ConnectionInformation
    {
        public Address DestinationAddress;

        public ApplicationNamingInfo DestinationApplication;

        public Address SourceAddress;

        public ApplicationNamingInfo SourceApplication;
    }

    /// <summary>
    /// This abstract class provides common implementation for all IPC classes.
    /// </summary>
    public abstract class IpcProcessBase : IRinaIpc
    {
        /// <summary>
        /// Safe way to create random and unique CepId.
        /// </summary>
        protected Helpers.UniqueRandomUInt64 _cepidSpace = new Helpers.UniqueRandomUInt64();

        /// <summary>
        /// Represents deallocated flows/connections.
        /// This map represents a function: LocalCepid -> ConnectionEndpoint.
        /// </summary>
        /// <remarks>
        /// Connections rest for a while in this map before they are removed. It is because some operations
        /// may be still in progress that would require an access to a connection information.
        /// </remarks>
        protected Dictionary<ulong, ConnectionEndpoint> _connectionsClosed = new Dictionary<ulong, ConnectionEndpoint>();

        protected Dictionary<ulong, ConnectionEndpoint> _connectionsClosing = new Dictionary<ulong, ConnectionEndpoint>();

        protected Dictionary<ulong, ConnectionEndpoint> _connectionsConnecting = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Represents allocated flows/connections.
        /// This map represents a function: LocalCepid -> ConnectionEndpoint.
        /// </summary>
        protected Dictionary<ulong, ConnectionEndpoint> _connectionsOpen = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// The <see cref="Address"/> of the current <see cref="PipeIpcProcess"/> instance.
        /// </summary>
        protected Address _localAddress;

        /// <summary>
        /// Helper that manages unique randomly generated port numbers.
        /// </summary>
        protected Helpers.UniqueRandomUInt32 _portidSpace = new Helpers.UniqueRandomUInt32();

        /// <summary>
        /// Tracks all created ports.
        /// </summary>
        protected Dictionary<ulong, Port> _portsAllocated = new Dictionary<ulong, Port>();

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        private Dictionary<Guid, RegisteredApplication> _registeredApplications = new Dictionary<Guid, RegisteredApplication>();

        private object _registeredApplicationsLock = new object();

        public enum WsaControlCode
        {
            /// <summary>
            /// Enable or disable non-blocking mode on connection end point (FIONBIO).
            /// </summary>
            NonBlocking,

            /// <summary>
            /// Discards current contents of the sending queue associated with this connection end point (SIO_FLUSH).
            /// </summary>
            Flush,

            /// <summary>
            /// Determine the amount of data that can be read atomically from connection end point. (FIONREAD)
            /// </summary>
            AvailableData
        }

        public Address LocalAddress { get { return _localAddress; } }

        public void Abort(Port port)
        {
            throw new NotImplementedException();
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
            var cep = wsaConnection(flowInfo.DestinationAddress.Family, ConnectionType.Rdm);

            var qos = new QosParameters();
            object remoteCepid = 0ul;
            var ci = new ConnectionInformation()
            {
                SourceAddress = flowInfo.SourceAddress,
                SourceApplication = flowInfo.SourceApplication,
                DestinationAddress = flowInfo.DestinationAddress,
                DestinationApplication = flowInfo.DestinationApplication
            };

            var error = wsaConnect(cep, ci, cep.LocalCepId, out remoteCepid, qos, qos);
            if (error == PortError.Success)
            {
                cep.Information = ci;

                cep.RemoteCepId = (ulong)remoteCepid;

                cep.Port = new Port(this, _portidSpace.Next())
                {
                    CepId = cep.LocalCepId
                };

                _connectionsOpen.Add(cep.LocalCepId, cep);
                return cep.Port;
            }
            else
            {
                return null;
            }
        }

        public bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                object value;
                wsaGetIoctl(cep, WsaControlCode.AvailableData, out value);
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
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                return cep.ReceiveQueue.OutputAvailableAsync(ct);
            }
            throw new PortException(PortError.InvalidArgument);
        }

        public void DeregisterApplication(ApplicationInstanceHandle appInfo, DeregisterApplicationOption option, TimeSpan timeout)
        {
            if (option == DeregisterApplicationOption.DisconnectClients) throw new NotSupportedException($"Option {option} is not supported. Only {DeregisterApplicationOption.WaitForCompletition} is currently supported.");
            lock (_registeredApplicationsLock)
            {
                _registeredApplications.Remove(appInfo.Handle);
            }
        }

        /// <summary>
        /// This gracefully shutdown the connection.
        /// </summary>
        /// <param name="port"></param>
        public void Disconnect(Port port, TimeSpan timeout)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                PortError errorCode = wsaSendDisconnect(cep);
                if (errorCode != PortError.Success) throw new PortException(errorCode);
            }
            else
                throw new PortException(PortError.InvalidArgument);
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                PortInformationOptions pi = 0;
                pi |= !cep.Blocking ? PortInformationOptions.NonBlocking : 0;
                pi |= cep.Connected ? PortInformationOptions.Connected : 0;
                return pi;
            }
            throw new PortException(PortError.InvalidArgument);
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
        public int Receive(Port port, byte[] buffer, int offset, int size, PortFlags socketFlags)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                int bytesReceived;
                var errorCode = wsaRecv(cep, buffer, offset, size, out bytesReceived, socketFlags);
                return bytesReceived;
            }
            else
                throw new PortException(PortError.InvalidArgument);
        }

        public ApplicationInstanceHandle RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            lock (_registeredApplicationsLock)
            {
                Trace.TraceInformation($"Application '{appInfo.ApplicationName}' registered at process {this.LocalAddress}.");

                var appHandle = new ApplicationInstanceHandle();
                this._registeredApplications.Add(appHandle.Handle, new RegisteredApplication() { Handle = appHandle, ApplicationInfo = appInfo, RequestHandler = reqHandler });
                return appHandle;
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int size)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                var error = wsaSend(cep, buffer, offset, size);
                if (error != PortError.Success)
                    throw new PortException(error);
                return size;
            }
            else
                throw new PortException(PortError.InvalidArgument);
        }

        public void SetBlocking(Port port, bool value)
        {
            ConnectionEndpoint cep;
            if (_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                cep.Blocking = value;
            }
        }

        protected RegisteredApplication FindApplication(string appName)
        {
            var appInfo = _registeredApplications.Values.FirstOrDefault(r => { return r.ApplicationInfo.ApplicationName.Equals(appName, StringComparison.InvariantCultureIgnoreCase); });
            return appInfo;
        }

        protected abstract PortError wsaCleanup();

        protected abstract PortError wsaConnect(ConnectionEndpoint cep, ConnectionInformation ci, object callerData, out object calleeData, QosParameters outflow, QosParameters inflow);

        protected abstract ConnectionEndpoint wsaConnection(Sockets.AddressFamily af, ConnectionType ct);

        protected abstract PortError wsaGetIoctl(ConnectionEndpoint cep, WsaControlCode code, out object value);

        protected abstract PortError wsaRecv(ConnectionEndpoint cep, byte[] buffer, int offset, int size, out int bytesReceived, PortFlags socketFlags);

        protected abstract PortError wsaSend(ConnectionEndpoint cep, byte[] buffer, int offset, int size);

        protected abstract PortError wsaSendDisconnect(ConnectionEndpoint cep);

        protected abstract PortError wsaSetIoctl(ConnectionEndpoint cep, WsaControlCode code, object value);

        protected abstract PortError wsaStartup(ushort versionRequested, out object data);

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        public void Close(Port port)
        {
            throw new NotImplementedException();
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

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
        // ~IpcProcessBase() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
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
}