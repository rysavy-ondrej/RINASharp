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
                SourceAddress = Address.FromWinIpcString(this.SourceAddress),
                DestinationAddress = Address.FromWinIpcString(this.DestinationAddress),
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
        /// <param name="connectionId">Id of connection associated with this opertation.</param>
        /// <param name="data">A buffer with data to be pushed to the remote site.</param>
        /// <return>Number of bytes accepted from the provided buffer.</return>
        [OperationContract]
        int PushData(UInt64 connectionId, byte []data);
        /// <summary>
        /// Attempts to pull data from remote site.
        /// </summary>
        /// <param name="connectionId">Id of connection associated with this opertation.</param>
        /// <param name="count">A number of bytes to get from the remote object.</param>
        /// <returns>A buffer with data. The size of this buffer is at most <c>count</c> bytes.</returns>
        [OperationContract]
        byte[] PullData(UInt64 connectionId, int count);

        /// <summary>
        /// Request a flow allocation.
        /// </summary>
        /// <param name="flowInfo">Information about flow.</param>
        /// <returns>A value that can be used as a connection Id for futher operations.</returns>
        [OperationContract]
        UInt64 OpenConnection(ConnectionInformation flowInfo);

        /// <summary>
        /// Closes the flow with the specified connection id.
        /// </summary>
        /// <param name="connectionId">>Id of connection associated with this opertation.</param>
        [OperationContract]
        void CloseConnection(UInt64 connectionId);
    }

   


    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs WCF Service model.
    /// </summary>
    [ShimIpc("WcfServiceIpc")]
    public class WcfServiceIpcProcess : IRinaIpc, IDisposable
	{
        Object m_connectionEndpointsLock = new Object();
        Object m_generalLock = new Object();
        ulong m_lastAllocatedPortId = 0;
        Thread m_IpcWorker;
        ServiceHost m_serviceHost;

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
            /// rmeote object is create as a singleton. Connection Id is set to PortId of the "server".
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
        MultiKeyDictionary<ulong, ulong, ConnectionEndpoint> m_ConnectionEndpoints = new MultiKeyDictionary<ulong, ulong, ConnectionEndpoint>();

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        List<RegisteredApplication> m_registeredApplications = new List<RegisteredApplication>();
        /// <summary>
        /// Maps flow ids to receive buffer objects.
        /// </summary>
        //Dictionary<ulong, FifoStream> _receiveBuffers = new Dictionary<ulong, FifoStream> ();

        public Address LocalAddress { get; private set; }

        /// <summary>
        /// Provides access to working thread. 
        /// </summary>
        public Thread Worker {  get { return this.m_IpcWorker; } }

        public static WcfServiceIpcProcess Create(string localAddress)
        {
            try
            {
                var ipc = new WcfServiceIpcProcess(localAddress);
                ipc.m_IpcWorker = new Thread(ipc.Run);
                ipc.m_IpcWorker.Start();
                return ipc;
            }
            catch(AddressAlreadyInUseException e)
            {
                var info = String.Format("Address already in use: {0}", e.Message);
                Trace.WriteLine(info, "ERROR");
                return null;
            }
        }

        /// <summary>
        /// A main loop of this IPC. This loop mamages the IPC. It detect inactive connections 
        /// and cleans up unused resources.
        /// </summary>
        void Run()
        {
            try {
                while (true)
                {
                    Thread.Sleep(1000);
                    var info = String.Format("{0}: Flow count {1}", DateTime.Now,m_ConnectionEndpoints.Count);
                    Trace.WriteLine(info, "INFO");
                }
            }
            catch(ThreadAbortException)
            { }
        }

        /// <summary>
        /// Creates a WinIpcContext object using the specified local address.
        /// </summary>
        /// <param name="localAddress">The name of the local IPC port.</param>
        WcfServiceIpcProcess(string localAddress)
        {
            LocalAddress = Address.FromWinIpcPort(localAddress);
            // create server side of channel...
            m_serviceHost = new ServiceHost(new RemoteObject(this), new Uri[]{new Uri((string)this.LocalAddress.Value)});
            m_serviceHost.AddServiceEndpoint(typeof(IRemoteObject), new NetNamedPipeBinding(), typeof(RemoteObject).ToString());
            m_serviceHost.Open();                        
        }

        /// <summary>
        /// Private method for allocating a new flow and filling all necessary tables.
        /// </summary>
        /// <param name="flowInstance">An instance of flow for which all the processing is perfomed.</param>
        /// <param name="remoteObject">The IWinIpcRinaObject object used for remote communication.</param>
        /// <returns>The Port object associated with newly allocated flow.</returns>
        private bool allocateFlow(FlowInformation flowInformation, out ConnectionEndpoint cep)
        {
            var port = new Port(this, ++m_lastAllocatedPortId);
            var localAddress = (string)this.LocalAddress.Value;
            var remoteAddres = (string)flowInformation.DestinationAddress.Value;
            // connect to the target IPC:
            var commObject = new ChannelFactory<IRemoteObject>(new NetNamedPipeBinding());
            var remoteObject = commObject.CreateChannel(new EndpointAddress(string.Format("{0}/{1}", remoteAddres, typeof(RemoteObject).ToString())));

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
            lock(m_connectionEndpointsLock)
            {
                var wipcPortname = (string)flowInformation.DestinationAddress.Value;              
                ConnectionEndpoint cep = null;
                if (this.allocateFlow(flowInformation, out cep))
                {
                    var connInfo = new ConnectionInformation()
                    {
                        SourceAddress = (string)this.LocalAddress.Value,
                        DestinationAddress = (string)flowInformation.DestinationAddress.Value,
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
                        Trace.WriteLine("Could not connect remote site: s" + e.Message, "ERROR");
                    }
                    if (cepId > 0)
                    {
                        cep.ConnectionId = cepId;
                        m_ConnectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
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
            lock(m_connectionEndpointsLock)
            {
                var flowInformation = connInfo.Reverse().GetFlowInformation();
                ConnectionEndpoint cep = null;
                if (allocateFlow(flowInformation, out cep))
                {
                    cep.ConnectionId = cep.Port.Id;                  
                    m_ConnectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
                    return cep.Port;
                }
                return null;
            }
        }

		public void DeallocateFlow (Port port)
		{
            lock(m_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (m_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.CommunicationObject.Close();
                    cep.ReceiveBuffer.Dispose();
                    m_ConnectionEndpoints.Remove(primaryKey: port.Id);
                }
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int count)
        {
            ConnectionEndpoint cep;
            if (m_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {                
                var dataToSent = new byte[count];
                Buffer.BlockCopy(buffer, offset, dataToSent, 0, count);
                var amount = cep.RemoteObject.PushData(cep.ConnectionId, dataToSent);
                return amount;
            }
            return -1;
        }

		public int Receive (Port port, byte[] buffer, int offset, int count)
		{
            ConnectionEndpoint cep;
            if (m_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                var data = cep.ReceiveBuffer;
                if (port.Blocking) data.WaitForData(Timeout.Infinite);
                var bytesToRead = Math.Min(data.AvailableBytes, count);
                return data.Read(buffer, offset, bytesToRead);
            }
            return -1;
		}
        

		public void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
		{
            lock(m_generalLock)
            {
                var info = String.Format("Application '{0}' registered.", appInfo.ProcessName);
                Trace.WriteLine(info, "INFO");
                this.m_registeredApplications.Add(new RegisteredApplication() { ApplicationInfo = appInfo, Handler = reqHandler });
            }
		}

        public void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            lock(m_generalLock)
            {
                this.m_registeredApplications.RemoveAll(x =>
                {
                    return appInfo.Matches(x.ApplicationInfo);
                });
            }
        }

        internal ulong ConnectToApplication(ConnectionInformation connectionInformation)
        {
            lock(m_generalLock)
            {
                var info = String.Format("Connection request to application '{0}'.", connectionInformation.DestinationProcessName);
                Trace.WriteLine(info, "INFO");

                var app = m_registeredApplications.Find(r => { return r.ApplicationInfo.ProcessName.Equals(connectionInformation.DestinationProcessName, StringComparison.InvariantCultureIgnoreCase); });
                if (app != null)
                {
                    AcceptFlowHandler afh = null;
                    var flowInfo = connectionInformation.Reverse().GetFlowInformation();
                    var reply = app.Handler(this, flowInfo, out afh);
                    info = String.Format("Request handler of application '{0}' replied {1}.", connectionInformation.DestinationProcessName, reply.ToString());
                    Trace.WriteLine(info, "INFO");
                    if (reply == ConnectionRequestResult.Accept)
                    {
                        // now it is the right time to create all the object necessary for further communication:
                        var port = this.AllocateFlow(connectionInformation);
                        afh(this, flowInfo, port);
                        return port.Id;
                    }
                    else
                        return 0;
                }
                else
                {
                    info = String.Format("Application '{0}' not found in local IPC.", connectionInformation.DestinationProcessName);
                    Trace.WriteLine(info, "ERROR");
                    return 0;
                }
            }
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            if (port == null) throw new ArgumentNullException("port");
            PortInformationOptions opt = 0;
            ConnectionEndpoint cep;
            if (m_ConnectionEndpoints.TryGetValue(primaryKey:port.Id, val: out cep))
            {
                opt |= cep.CommunicationObject.State == CommunicationState.Opened ? PortInformationOptions.Connected : 0;
                opt |= cep.Blocking ? 0 :  PortInformationOptions.NonBlocking;
            }
            return opt;
        }

        public int GetReceiveBufferSize(Port port)
        {
            throw new NotImplementedException();
        }

        public void SetReceiveBufferSize(Port port, int size)
        {
            throw new NotImplementedException();
        }

        public int GetSendBufferSize(Port port)
        {
            throw new NotImplementedException();
        }
                                                                                                                           
        public void SetSendBufferSize(Port port, int size)
        {
            throw new NotImplementedException();
        }

        private bool m_disposedValue = false; // To detect redundant calls
        private object m_disposeLock = new object();
        protected virtual void Dispose(bool disposing) 
        {
            lock(m_disposeLock)
            {
                if (!m_disposedValue)
                {
                    if (disposing)
                    {
                        var info = String.Format("Shutting down worker thread '{0}'.", this.m_IpcWorker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        this.m_IpcWorker.Abort();

                        info = String.Format("Waiting for thread '{0}' to finish.", this.m_IpcWorker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        this.m_IpcWorker.Join();

                        info = String.Format("Thread '{0}' finished. Disposing managed resources of the current IPC.", this.m_IpcWorker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        closeServiceHost();

                        info = String.Format("IPC disposed.");
                        Trace.WriteLine(info, "INFO");
                    }

                    this.m_registeredApplications = null;
                    this.m_ConnectionEndpoints.Clear();
                    m_disposedValue = true;
                }
            }
        }
        
        private void closeServiceHost()
        {
            if (m_serviceHost.State == CommunicationState.Opened)
            {
                var info = String.Format("Closing IPC service host (State='{0}').", this.m_serviceHost.State);
                Trace.WriteLine(info, "INFO");
                foreach (var x in m_ConnectionEndpoints)
                {
                    var cep = x.Value;                                        
                    try {
                        cep.RemoteObject.CloseConnection(cep.ConnectionId);
                        cep.CommunicationObject.Close();
                    } catch (Exception)
                    {
                        info = String.Format("Remote site is not available (Address='{0}', Application='{1}').", 
                            cep.FlowInformation.DestinationAddress, cep.FlowInformation.DestinationApplication, cep.ConnectionId);
                        Trace.WriteLine(info, "INFO");
                    }                
                }
            }
            m_serviceHost.Close();            
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        public void SetBlocking(Port port, bool value)
        {
            lock(m_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (m_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.Blocking = value;                
                }
            }
        }

        public int AvailableData(Port port)
        {
            ConnectionEndpoint cep;
            if (m_ConnectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveBuffer.AvailableBytes;
            }
            return -1;
        }


        #region Nested classes
        internal class RegisteredApplication
        {
            internal ApplicationNamingInfo ApplicationInfo;
            internal ConnectionRequestHandler Handler;
        }

        /// <summary>
        /// Implements a RINA remote object for Windows IPC Shim DIF. It is always created as a singleton in the application domain.
        /// </summary>
        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
        internal class RemoteObject : IRemoteObject
        {
            WcfServiceIpcProcess _parentObject;
            ChannelFactory<IRemoteObject> _channel;
            public RemoteObject(WcfServiceIpcProcess parent)
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

                return this._parentObject.ConnectToApplication(connectionInformation);
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
                if (_parentObject.m_ConnectionEndpoints.TryGetValue(subKey: connectionId, val: out cep))
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