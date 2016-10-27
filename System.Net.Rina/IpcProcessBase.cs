using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina
{
    /// <summary>
    /// Defines control code for 
    /// <see cref="IpcProcessBase.wsaSetIoctl(ConnectionEndpoint, WsaControlCode, object)"/> 
    /// and
    /// <see cref="IpcProcessBase.wsaGetIoctl(ConnectionEndpoint, WsaControlCode, out object)"/>.
    /// </summary>
    public enum WsaControlCode
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
        protected CepIdSpace m_cepidSpace = new CepIdSpace();

        /// <summary>
        /// Represents deallocated connections.
        /// This map represents a function: LocalCepid -> ConnectionEndpoint.
        /// </summary>
        /// <remarks>
        /// Connections rest for a while in this map before they are removed. It is because some operations
        /// may be still in progress that would require an access to a connection information.
        /// </remarks>
        protected Dictionary<ulong, ConnectionEndpoint> m_connectionsClosed = new Dictionary<ulong, ConnectionEndpoint>();

        protected Dictionary<ulong, ConnectionEndpoint> m_connectionsClosing = new Dictionary<ulong, ConnectionEndpoint>();

        protected Dictionary<ulong, ConnectionEndpoint> m_connectionsConnecting = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// Represents allocated flows/connections.
        /// This map represents a function: LocalCepid -> ConnectionEndpoint.
        /// </summary>
        protected Dictionary<ulong, ConnectionEndpoint> m_connectionsOpen = new Dictionary<ulong, ConnectionEndpoint>();

        /// <summary>
        /// The <see cref="Address"/> of the current <see cref="PipeIpcProcess"/> instance.
        /// </summary>
        protected Address m_localAddress;

        /// <summary>
        /// Helper that manages unique randomly generated port numbers.
        /// </summary>
        protected PortIdSpace m_portidSpace = new PortIdSpace();

        /// <summary>
        /// Tracks all created ports.
        /// </summary>
        protected Dictionary<ulong, Port> m_portsAllocated = new Dictionary<ulong, Port>();

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        private Dictionary<Guid, RegisteredApplication> m_registeredApplications = new Dictionary<Guid, RegisteredApplication>();

        private object m_registeredApplicationsLock = new object();

        public Address LocalAddress { get { return m_localAddress; } }

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
            var ctype = GetConnectionType(flowInfo);

            var cep = wsaConnection(flowInfo.DestinationAddress.Family, ConnectionType.Rdm);
            
            
            var ci = new ConnectionInformation()
            {
                SourceAddress = flowInfo.SourceAddress,
                SourceApplication = flowInfo.SourceApplication,
                DestinationAddress = flowInfo.DestinationAddress,
                DestinationApplication = flowInfo.DestinationApplication
            };
            CepIdType remoteCepid;
            var qos = SelectQosParameters(flowInfo);

            var error = wsaConnect(cep, ci, cep.LocalCepId, out remoteCepid, qos, qos);
            if (error == PortError.Success)
            {
                return wsaBind(cep, ci, remoteCepid);               
            }
            else
            {
                wsaClose(cep);
                return null;
            }
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
        protected Port wsaBind(ConnectionEndpoint cep, ConnectionInformation ci, CepIdType remoteCepId)
        {
            cep.RemoteCepId = remoteCepId;
            cep.Information = ci;                    
            cep.Port = new Port(this, m_portidSpace.Next())
            {
                CepId = cep.LocalCepId
            };
            cep.Connected = true;
            m_connectionsOpen.Add(cep.LocalCepId, cep);
            m_portsAllocated.Add(cep.Port.Id, cep.Port);
            return cep.Port;
        }


        private QosParameters SelectQosParameters(FlowInformation flowInfo)
        {
            return new QosParameters();
        }

        private ConnectionType GetConnectionType(FlowInformation flowInfo)
        {
            return ConnectionType.Rdm;
        }

        public bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                object value;
                wsaIoctl(cep, WsaControlCode.AvailableData, null, out value);
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
                wsaIoctl(cep, WsaControlCode.AvailableDataTask, ct, out value);

                return value as Task<bool>;
            }
            throw new PortException(PortError.InvalidArgument);
        }

        public void DeregisterApplication(ApplicationInstanceHandle appInfo, DeregisterApplicationOption option, TimeSpan timeout)
        {
            if (option == DeregisterApplicationOption.DisconnectClients) throw new NotSupportedException($"Option {option} is not supported. Only {DeregisterApplicationOption.WaitForCompletition} is currently supported.");
            lock (m_registeredApplicationsLock)
            {
                m_registeredApplications.Remove(appInfo.Handle);
            }
        }


        /// <summary>
        /// This gracefully shutdown the connection.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="timeout">Specifies the time interval during which the process waits for DisconnectResponse.</param>
        /// <returns>true if operation completed or false if timeout.</returns>
        public bool Disconnect(Port port, TimeSpan timeout)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                PortError errorCode = wsaSendDisconnect(cep);
                if (errorCode != PortError.Success) throw new PortException(errorCode);

                // wait for DisconnectResponse:
                try
                {
                    cep.DisconnectResponseReceived.Wait(timeout);
                    return true;
                }
                catch(TimeoutException)
                {
                    return false;
                }
            }
            else
                throw new PortException(PortError.InvalidArgument);
        }


        /// <summary>
        /// Called by the derived class when disconnect request message was received. 
        /// If disconnect request contains Abort flag then all buffered data 
        /// in SEND queue will be removed. Otherwise, it is possible to send 
        /// the remaining content of the buffer. 
        /// The RECEIVE buffer will be closed for further messages but
        /// the application may read available bytes. 
        /// </summary>
        /// <param name="cep"></param>
        /// <param name="abortRequested"></param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation and completion of the disconnect request.</returns>
        protected Task<bool> OnDisconnectRequest(ConnectionEndpoint cep, bool abortRequested = false)
        {
            // TODO: What to do? 
            // i)  Discard the queue content
            // ii) Let the IPC to process the remaining data            
            if (abortRequested)
            {                
                m_connectionsOpen.Remove(cep.Id);                
                m_connectionsClosed.Add(cep.Id, cep);
                cep.State = ConnectionState.Closed;
                return Task<bool>.Run(() => true);
            }
            else
            {
                m_connectionsOpen.Remove(cep.Id);                
                m_connectionsClosing.Add(cep.Id, cep);
                cep.State = ConnectionState.Closing;
                return Task<bool>.Run(() =>
                {
                    cep.SendCompletition.Wait();
                    return true;
                });
            }
        }
        protected void OnDisconnectResponse(ConnectionEndpoint cep)
        {
            m_connectionsOpen.Remove(cep.Id);
            m_connectionsClosed.Add(cep.Id, cep);
            cep.State = ConnectionState.Closed;
            cep.DisconnectResponseReceived.Set();                        
        }


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
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep) || m_connectionsClosing.TryGetValue(port.CepId, out cep))
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
            lock (m_registeredApplicationsLock)
            {
                Trace.TraceInformation($"Application '{appInfo.ApplicationName}' registered at process {this.LocalAddress}.");

                var appHandle = new ApplicationInstanceHandle();
                this.m_registeredApplications.Add(appHandle.Handle, new RegisteredApplication() { Handle = appHandle, ApplicationInfo = appInfo, RequestHandler = reqHandler });
                return appHandle;
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int size)
        {
            ConnectionEndpoint cep;
            if (m_connectionsOpen.TryGetValue(port.CepId, out cep))
            {
                    var error = wsaSend(cep, buffer, offset, size);
                    if (error != PortError.Success)
                        throw new PortException(error);
                    return size;
            }
            else
                throw new PortException(PortError.NotConnected);
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
        /// Gets the <see cref="ConnectionState"/> of the connection end point specified by the <paramref name="cepid"/>.
        /// </summary>
        /// <param name="cepid"></param>
        /// <returns>One of the connection state as defined by <see cref="ConnectionState"/>. </returns>
        internal ConnectionState GetConnectionState(UInt64 cepid)
        {
            if (m_connectionsOpen.ContainsKey(cepid)) return ConnectionState.Open;
            if (m_connectionsClosed.ContainsKey(cepid)) return ConnectionState.Closed;
            if (m_connectionsConnecting.ContainsKey(cepid)) return ConnectionState.Connecting;
            if (m_connectionsClosing.ContainsKey(cepid)) return ConnectionState.Closing;
            return ConnectionState.Detached;
        }
        protected RegisteredApplication FindApplication(string appName)
        {
            var appInfo = m_registeredApplications.Values.FirstOrDefault(r => { return r.ApplicationInfo.ApplicationName.Equals(appName, StringComparison.InvariantCultureIgnoreCase); });
            return appInfo;
        }

        /// <summary>
        /// The wsaCleanup function terminates the use of the IPC process. Override this function 
        /// to implement proper handling of events related to shutting down the current IPC.
        /// </summary>
        /// <returns>A result of the operation as a <see cref="PortError"/> value.</returns>
        protected abstract PortError wsaCleanup();

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
        /// <returns>A result of the operation as a <see cref="PortError"/> value.</returns>
        protected abstract PortError wsaConnect(ConnectionEndpoint cep, ConnectionInformation ci, CepIdType callerCepId, out CepIdType calleeCepId, QosParameters outflow, QosParameters inflow);
        /// <summary>
        /// This method creates a new <see cref="ConnectionEndpoint"/>. Override this method to
        /// provide the object of the required type.
        /// </summary>
        /// <param name="af">Address family of the <see cref="ConnectionEndpoint"/>.</param>
        /// <param name="ct">Connection type of the <see cref="ConnectionEndpoint"/>.</param>
        /// <returns>
        /// The method may return null if either <see cref="Sockets.AddressFamily"/> nor <see
        /// cref="ConnectionType"/> is not supported.
        /// </returns>
        protected abstract ConnectionEndpoint wsaConnection(Sockets.AddressFamily af, ConnectionType ct);

        /// <summary>
        /// Called before the port bound to the specified <see cref="ConnectionEndpoint"/> is closed. 
        /// </summary>
        /// <param name="cep"><see cref="ConnectionEndpoint"/> which should be closed.</param>
        /// <returns>A result of the operation as a <see cref="PortError"/> value.</returns>
        protected abstract PortError wsaClose(ConnectionEndpoint cep);

        /// <summary>
        /// Gets 
        /// </summary>
        /// <param name="cep"></param>
        /// <param name="code"></param>
        /// <param name="param"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected abstract PortError wsaIoctl(ConnectionEndpoint cep, WsaControlCode code, object inValue, out object outValue);

        protected abstract PortError wsaRecv(ConnectionEndpoint cep, byte[] buffer, int offset, int size, out int bytesReceived, PortFlags socketFlags);

        protected abstract PortError wsaSend(ConnectionEndpoint cep, byte[] buffer, int offset, int size);

        protected abstract PortError wsaSendDisconnect(ConnectionEndpoint cep);

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