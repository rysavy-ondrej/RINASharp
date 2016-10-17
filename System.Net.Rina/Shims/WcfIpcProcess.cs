//
//  WinIpcShimDif.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE Consortium (http://ict-pristine.eu)
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System.IO;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace System.Net.Rina.Shims
{
    /// <summary>
    /// This class is used to hold properties of a new connection requested by the client side.
    /// Information in this class is used to identify originator of the request as well as to 
    /// find an application that should serve the request.
    /// </summary>
    [DataContract]
    public class ConnectionInformation
    {
        /// <summary>
        /// Represents a source Windows IPC port name.
        /// </summary>
        [DataMember]
        public string SourceAddress { get; set; }
        /// <summary>
        /// Represents a destination Windows IPC port name.
        /// </summary>
        [DataMember]
        public string DestinationAddress { get; set; }
        /// <summary>
        /// Represents a source application naming information.
        /// </summary>
        [DataMember]
        public string SourceProcessName { get; set; }
        [DataMember]
        public string SourceProcessInstance { get; set; }
        [DataMember]
        public string SourceEntityName { get; set; }
        [DataMember]
        public string SourceEntityInstance { get; set; }
        /// <summary>
        /// Represents a destination application naming information.
        /// </summary>
        [DataMember]
        public string DestinationProcessName { get; set; }
        [DataMember]
        public string DestinationProcessInstance { get; set; }
        [DataMember]
        public string DestinationEntityName { get; set; }
        [DataMember]
        public string DestinationEntityInstance { get; set; }

        public ConnectionInformation Reverse()
        {
            var rev = new ConnectionInformation()
            {
                SourceAddress = this.DestinationAddress,
                DestinationAddress = this.SourceAddress,
                SourceProcessName = this.DestinationProcessName,
                SourceProcessInstance = this.DestinationProcessInstance,
                SourceEntityName = this.DestinationEntityName,
                SourceEntityInstance = this.DestinationEntityInstance,
                DestinationProcessName = this.SourceProcessName,
                DestinationProcessInstance = this.SourceProcessInstance,
                DestinationEntityName = this.SourceEntityName,
                DestinationEntityInstance = this.SourceEntityInstance
            };
            return rev;
            
        }

        /// <summary>
        /// Gets a flow information object from the current connection information object.
        /// </summary>
        /// <returns>A FlowInformation object.</returns>
        public FlowInformation GetFlowInformation()
        {
            var flowInfo = new FlowInformation()
            {
                SourceAddress = new Address(new Uri(this.SourceAddress)),
                DestinationAddress = new Address(new Uri(this.DestinationAddress)),
                SourceApplication = new ApplicationNamingInfo(this.SourceProcessName, this.SourceProcessInstance, this.SourceEntityName, this.SourceEntityInstance),
                DestinationApplication = new ApplicationNamingInfo(this.DestinationProcessName, this.DestinationProcessInstance, this.DestinationEntityName, this.DestinationEntityInstance)
            };
            return flowInfo;
        }
    }

    /// <summary>
    /// This specifies an interface of a remote IPC object that is used to serve all connection requests.
    /// </summary>
    [ServiceContract]
    internal interface IRemoteObject
    {
        /// <summary>
        /// Pushes data to the target object. 
        /// </summary>
        /// <param name="connectionId">Id of connection associated with this operation.</param>
        /// <param name="data">A buffer with data to be pushed to the remote site.</param>
        /// <return>Number of bytes accepted from the provided buffer.</return>
        [OperationContract]
        int PushData(UInt64 connectionId, byte []data);
        /// <summary>
        /// Attempts to pull data from remote site.
        /// </summary>
        /// <param name="connectionId">Id of connection associated with this operation.</param>
        /// <param name="count">A number of bytes to get from the remote object.</param>
        /// <returns>A buffer with data. The size of this buffer is at most <c>count</c> bytes.</returns>
        [OperationContract]
        byte[] PullData(UInt64 connectionId, int count);

        /// <summary>
        /// Request a flow allocation.
        /// </summary>
        /// <param name="flowInfo">Information about flow.</param>
        /// <returns>A value that can be used as a connection Id for further operations.</returns>
        [OperationContract]
        UInt64 OpenConnection(ConnectionInformation flowInfo);

        /// <summary>
        /// Closes the flow with the specified connection id.
        /// </summary>
        /// <param name="connectionId">>Id of connection associated with this operation.</param>
        [OperationContract]
        void CloseConnection(UInt64 connectionId);
    }

   


    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs WCF Service model.
    /// </summary>
    [ShimIpc("WcfService")]
    public class WcfIpcProcess : IRinaIpc
	{
        object _connectionEndpointsLock = new object();
        object _generalLock = new object();
        ulong _lastAllocatedPortId = 0;
        ServiceHost _serviceHost;

        internal class ConnectionEndpoint
        {
            /// <summary>
            /// Stores the local Port object.
            /// </summary>
            internal Port Port;
            /// <summary>
            /// Specifies whether the port is blocking or non-blocking.
            /// </summary>
            internal bool Blocking;
            /// <summary>
            /// Flow information that describes parameters of the current connection.
            /// </summary>
            internal FlowInformation FlowInformation;
            /// <summary>
            /// Connection Id is used to resolve to which connection data should be written as 
            /// remote object is create as a singleton. Connection Id is set to PortId of the "server".
            /// </summary>
            internal ulong ConnectionId;
            /// <summary>
            /// Object used to connect to the remote object.
            /// </summary>
            internal ICommunicationObject CommunicationObject;
            /// <summary>
            /// Remote object that is used for sending and received data.
            /// </summary>
            internal IRemoteObject RemoteObject;
            /// <summary>
            /// Local receive buffer. All data that are supposed for this flow are stored in this buffer.
            /// </summary>
            internal FifoStream ReceiveBuffer;
        }

        /// <summary>
        /// This maps the port id to connection endpoint object that maintains all necessary information for a flow.
        /// </summary>
        MultiKeyDictionary<ulong, ulong, ConnectionEndpoint> _ConnectionEndpoints = new MultiKeyDictionary<ulong, ulong, ConnectionEndpoint>();

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        List<RegisteredApplication> _registeredApplications = new List<RegisteredApplication>();
        /// <summary>
        /// Maps flow ids to receive buffer objects.
        /// </summary>
        //Dictionary<ulong, FifoStream> _receiveBuffers = new Dictionary<ulong, FifoStream> ();

        public Address LocalAddress { get; private set; }

        public static WcfIpcProcess Create(string localAddress)
        {
            try
            {
                var ipc = new WcfIpcProcess(localAddress);
                return ipc;
            }
            catch(AddressAlreadyInUseException e)
            {
                Trace.WriteLine($"Address already in use: {e.Message}", "ERROR");
                throw new AddressAlreadyInUseException($"specified address 'localAddress' is unavailable because it is already in use.");
            }
        }

        /// <summary>
        /// Creates a WinIpcContext object using the specified local address.
        /// </summary>
        /// <param name="localAddress">The name of the local IPC port.</param>
        WcfIpcProcess(string localAddress)
        {
            LocalAddress = Address.PipeAddressUri("localhost",localAddress);
            // create server side of channel...
            _serviceHost = new ServiceHost(new RemoteObject(this), new Uri[]{this.LocalAddress.Value as Uri});
            _serviceHost.AddServiceEndpoint(typeof(IRemoteObject), new NetNamedPipeBinding(), typeof(RemoteObject).ToString());
            _serviceHost.Open();                        
        }

        /// <summary>
        /// Private method for allocating a new flow and filling all necessary tables.
        /// </summary>
        /// <param name="flowInstance">An instance of flow for which all the processing is performed.</param>
        /// <param name="remoteObject">The IWinIpcRinaObject object used for remote communication.</param>
        /// <returns>The Port object associated with newly allocated flow.</returns>
        private bool allocateFlow(FlowInformation flowInformation, out ConnectionEndpoint cep)
        {
            var port = new Port(this, ++_lastAllocatedPortId);
            var localAddress = this.LocalAddress.Value.ToString();
            var remoteAddress = (string)flowInformation.DestinationAddress.Value.ToString();
            // connect to the target IPC:
            var commObject = new ChannelFactory<IRemoteObject>(new NetNamedPipeBinding());
            var remoteObject = commObject.CreateChannel(new EndpointAddress($"{remoteAddress}/{typeof(RemoteObject).ToString()}"));

            var receiveBuffer = new FifoStream(8192);
            cep = new ConnectionEndpoint()
            {
                Blocking = true,
                FlowInformation = flowInformation,
                Port = port,
                ReceiveBuffer = receiveBuffer,
                CommunicationObject = commObject,
                RemoteObject = remoteObject
            };            
            return true;
        }
            /// <summary>
            /// Allocates a new flow based on provided flowInformation. This is used by the initiator of the flow request.
            /// </summary>
            /// <param name="flowInformation"></param>
            /// <returns>The Port object that can be used for communication with remote party.</returns>
        public Port AllocateFlow (FlowInformation flowInformation)
		{
            lock(_connectionEndpointsLock)
            {
                var wipcPortname = flowInformation.DestinationAddress.Value.ToString();              
                ConnectionEndpoint cep = null;
                if (this.allocateFlow(flowInformation, out cep))
                {
                    var connInfo = new ConnectionInformation()
                    {
                        SourceAddress = LocalAddress.Value.ToString(),
                        DestinationAddress = flowInformation.DestinationAddress.Value.ToString(),
                        SourceProcessName = flowInformation.SourceApplication.ProcessName,
                        SourceProcessInstance = flowInformation.SourceApplication.ProcessInstance,
                        SourceEntityName = flowInformation.SourceApplication.EntityName,
                        SourceEntityInstance = flowInformation.SourceApplication.EntityInstance,
                        DestinationProcessName = flowInformation.DestinationApplication.ProcessName,
                        DestinationProcessInstance = flowInformation.DestinationApplication.ProcessInstance,
                        DestinationEntityName = flowInformation.DestinationApplication.EntityName,
                        DestinationEntityInstance = flowInformation.DestinationApplication.EntityInstance
                    };
                    ulong cepId = 0;
                    // ask the other side to open connection
                    try
                    {
                        cepId = cep.RemoteObject.OpenConnection(connInfo);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"Could not connect to remote site: {e.Message}", "ERROR");
                    }
                    if (cepId > 0)
                    {
                        cep.ConnectionId = cepId;
                        _ConnectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
                        return cep.Port;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// This method is called by the responder when flow, port and other necessary object should be created
        /// for accepted request.
        /// </summary>
        /// <param name="connInfo"></param>
        /// <returns></returns>
        internal Port AllocateFlow(ConnectionInformation connInfo)
        {
            lock(_connectionEndpointsLock)
            {
                var flowInformation = connInfo.Reverse().GetFlowInformation();
                ConnectionEndpoint cep = null;
                if (allocateFlow(flowInformation, out cep))
                {
                    cep.ConnectionId = cep.Port.Id;                  
                    _ConnectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
                    return cep.Port;
                }
                return null;
            }
        }

		public void DeallocateFlow (Port port)
		{
            lock(_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.CommunicationObject.Close();
                    cep.ReceiveBuffer.Dispose();
                    _ConnectionEndpoints.Remove(primaryKey: port.Id);
                }
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int count)
        {
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {                
                var dataToSent = new byte[count];
                Buffer.BlockCopy(buffer, offset, dataToSent, 0, count);
                var amount = cep.RemoteObject.PushData(cep.ConnectionId, dataToSent);
                return amount;
            }
            return -1;
        }

		public byte[] Receive (Port port)
		{
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                var data = cep.ReceiveBuffer;
                if (port.Blocking) data.WaitForData(Timeout.Infinite);
                var buffer = new byte[data.AvailableBytes];
                data.Read(buffer, 0, buffer.Length);
                return buffer;
            }
            return null;
		}
        

		public void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
		{
            lock(_generalLock)
            {
                Trace.WriteLine($"Application '{appInfo.ProcessName}' registered at process {this.LocalAddress}.", "INFO");
                this._registeredApplications.Add(new RegisteredApplication() { ApplicationInfo = appInfo, RequestHandler = reqHandler });
            }
		}

        public void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            lock(_generalLock)
            {
                this._registeredApplications.RemoveAll(x =>
                {
                    return appInfo.Matches(x.ApplicationInfo);
                });
            }
        }

        /// <summary>
        /// Connects flow to the registered application IPC. Application can refuse the connection or provide a handler for
        /// serving the requests. 
        /// </summary>
        /// <param name="connectionInformation">ConnectionInformation with hold properties of a new connection requested by the client side.</param>
        /// <returns>A <see cref="Port"/> newly created for the communication.</returns>
        internal Port ConnectToApplication(ConnectionInformation connectionInformation)
        {
            lock(_generalLock)
            {
                Trace.WriteLine($"Connection request to application '{connectionInformation.DestinationProcessName}'.", "INFO");

                var app = _registeredApplications.Find(r => { return r.ApplicationInfo.ProcessName.Equals(connectionInformation.DestinationProcessName, StringComparison.InvariantCultureIgnoreCase); });
                if (app != null)
                {
                    AcceptFlowHandler flowHandler = null;
                    var flowInfo = connectionInformation.Reverse().GetFlowInformation();
                    var reply = ConnectionRequestResult.Reject;
                    try
                    {
                        reply = app.RequestHandler(this, flowInfo, out flowHandler);
                    }
                    catch(Exception e)
                    {
                        Trace.WriteLine($"RequestHandler method of {app.ApplicationInfo.ProcessName}:{e.Message}", "ERROR");
                    }
                    Trace.WriteLine($"Request handler of application '{connectionInformation.DestinationProcessName}' replied {reply}.", "INFO");
                    if (reply == ConnectionRequestResult.Accept)
                    {
                        // now it is the right time to create all the object necessary for further communication:
                        var port = AllocateFlow(connectionInformation);

                        // excutes the handler on newly created Task
                        // this taks is running on its own...                   
                        Task.Run(async () => 
                        {
                            try { await flowHandler(this, flowInfo, port); }
                            catch (Exception e) { Trace.WriteLine($"FlowHandler method of {app.ApplicationInfo.ProcessName}: {e.Message}", "ERROR"); }

                        }).ConfigureAwait(false);

                        return port;
                    }
                    else
                        return null;
                }
                else
                {
                    Trace.WriteLine($"Application '{connectionInformation.DestinationProcessName}' not found in local IPC.", "ERROR");
                    return null;
                }
            }
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            if (port == null) throw new ArgumentNullException("port");
            PortInformationOptions opt = 0;
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey:port.Id, val: out cep))
            {
                opt |= cep.CommunicationObject.State == CommunicationState.Opened ? PortInformationOptions.Connected : 0;
                opt |= cep.Blocking ? 0 :  PortInformationOptions.NonBlocking;
            }
            return opt;
        }

        private bool _disposedValue = false; // To detect redundant calls
        private object _disposeLock = new object();
        protected virtual void Dispose(bool disposing) 
        {
            lock(_disposeLock)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                        closeServiceHost();
                        Trace.WriteLine("IPC disposed.", "INFO");
                    }

                    this._registeredApplications = null;
                    this._ConnectionEndpoints.Clear();
                    _disposedValue = true;
                }
            }
        }
        
        private void closeServiceHost()
        {
            if (_serviceHost.State == CommunicationState.Opened)
            {                
                Trace.WriteLine($"Closing IPC service host (State='{this._serviceHost.State}').", "INFO");
                foreach (var x in _ConnectionEndpoints)
                {
                    var cep = x.Value;                                        
                    try {
                        cep.RemoteObject.CloseConnection(cep.ConnectionId);
                        cep.CommunicationObject.Close();
                    } catch (Exception)
                    {
                        Trace.WriteLine($"Remote site is not available (Address='{cep.FlowInformation.DestinationAddress}', Application='{cep.FlowInformation.DestinationApplication}').", "INFO");
                    }                
                }
            }
            _serviceHost.Close();            
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        public void SetBlocking(Port port, bool value)
        {
            lock(_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.Blocking = value;                
                }
            }
        }

        public int AvailableData(Port port)
        {
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveBuffer.AvailableBytes;
            }
            return -1;
        }

        public Task<bool> DataAvailableAsync(Port port, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return Task.Factory.StartNew(() => cep.ReceiveBuffer.WaitForData(ct));
            }
            return Task.Factory.StartNew(() => false);
        }

        public bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveBuffer.AvailableBytes != 0;
            }
            return false;
        }


        #region Nested classes
        internal class RegisteredApplication
        {
            internal ApplicationNamingInfo ApplicationInfo;
            internal ConnectionRequestHandler RequestHandler;
        }

        /// <summary>
        /// Implements a RINA remote object for Windows IPC Shim DIF. It is always created as a singleton in the application domain.
        /// </summary>
        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
        internal class RemoteObject : IRemoteObject
        {
            WcfIpcProcess _parentObject;
            ChannelFactory<IRemoteObject> _channel;
            public RemoteObject(WcfIpcProcess parent)
            {
                this._parentObject = parent;
            }

            public void AssociateChannel(ChannelFactory<IRemoteObject> channelFactory)
            {
                _channel = channelFactory;
            }

            public void Close()
            {
                _channel.Close();
            }

            public void CloseConnection(ulong connectionId)
            {
                var info = String.Format("Connection '{0}' closed.", connectionId);
                Trace.WriteLine(info, "INFO");
            }

            public ulong OpenConnection(ConnectionInformation connectionInformation)
            {
                var preInfo = String.Format("Open connection request ('{0}'<->'{1}').", connectionInformation.SourceAddress, connectionInformation.DestinationAddress);
                Trace.WriteLine(preInfo, "INFO");

                return (ulong)this._parentObject.ConnectToApplication(connectionInformation)?.Id;
            }

            public byte[] PullData(ulong connectionId, int count)
            {
                throw new NotSupportedException("Pull mode of data transfer is not supported in WinIpcObject class.");
            }

            public int PushData(ulong connectionId, byte[] data)
            {
                var info = String.Format("PushData on connection '{0}'.", connectionId);
                Trace.WriteLine(info, "INFO");

                ConnectionEndpoint cep;
                if (_parentObject._ConnectionEndpoints.TryGetValue(subKey: connectionId, val: out cep))
                {
                    var recvBuffer = cep.ReceiveBuffer;
                    recvBuffer.Write(data, 0, data.Length);

                    info = String.Format("Receiving buffer now contains {0}bytes.", recvBuffer.AvailableBytes);
                    Trace.WriteLine(info, "INFO");
                    return data.Length;
                }
                return -1;
            }
        }
        #endregion
    }
}