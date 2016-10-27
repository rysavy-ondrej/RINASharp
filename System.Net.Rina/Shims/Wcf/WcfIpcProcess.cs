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
    /// This specifies an interface of a remote IPC object that is used to serve all connection requests.
    /// </summary>
    [ServiceContract]
    internal interface IRemoteObject
    {
        /// <summary>
        /// Closes the flow with the specified connection id.
        /// </summary>
        /// <param name="connectionId">>Id of connection associated with this operation.</param>
        [OperationContract]
        void CloseConnection(UInt64 connectionId);

        /// <summary>
        /// Request a flow allocation.
        /// </summary>
        /// <param name="flowInfo">Information about flow.</param>
        /// <returns>A value that can be used as a connection Id for further operations.</returns>
        [OperationContract]
        UInt64 OpenConnection(WcfIpcProcess.ConnectionInformation flowInfo);

        /// <summary>
        /// Attempts to pull data from remote site.
        /// </summary>
        /// <param name="connectionId">Id of connection associated with this operation.</param>
        /// <param name="count">A number of bytes to get from the remote object.</param>
        /// <returns>A buffer with data. The size of this buffer is at most <c>count</c> bytes.</returns>
        [OperationContract]
        byte[] PullData(UInt64 connectionId, int count);

        /// <summary>
        /// Pushes data to the target object. 
        /// </summary>
        /// <param name="connectionId">Id of connection associated with this operation.</param>
        /// <param name="data">A buffer with data to be pushed to the remote site.</param>
        /// <return>Number of bytes accepted from the provided buffer.</return>
        [OperationContract]
        int PushData(UInt64 connectionId, byte []data);
    }

   


    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs WCF Service model.
    /// </summary>
    [ShimIpc("WcfService")]
    public class WcfIpcProcess : IRinaIpc
	{
        /// <summary>
        /// This maps the port id to connection endpoint object that maintains all necessary information for a flow.
        /// </summary>
        MultiKeyDictionary<ulong, ulong, ConnectionEndpoint> m_connectionEndpoints = new MultiKeyDictionary<ulong, ulong, ConnectionEndpoint>();

        object m_connectionEndpointsLock = new object();
        private bool m_disposedValue = false;
        // To detect redundant calls
        private object m_disposeLock = new object();

        object m_generalLock = new object();

        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        List<RegisteredApplication> m_registeredApplications = new List<RegisteredApplication>();

        ServiceHost m_serviceHost;

        PortIdSpace m_portidSpace = new PortIdSpace();

        /// <summary>
        /// Creates a WinIpcContext object using the specified local address.
        /// </summary>
        /// <param name="localAddress">The name of the local IPC port.</param>
        WcfIpcProcess(string localAddress)
        {
            LocalAddress = Address.PipeAddressUri("localhost", localAddress);
            // create server side of channel...
            m_serviceHost = new ServiceHost(new RemoteObject(this), new Uri[] { this.LocalAddress.Value as Uri });
            m_serviceHost.AddServiceEndpoint(typeof(IRemoteObject), new NetNamedPipeBinding(), typeof(RemoteObject).ToString());
            m_serviceHost.Open();
        }

        public IpcHost Host { get; set; }
        public Address LocalAddress { get; private set; }
        /// <summary>
        /// Maps flow ids to receive buffer objects.
        /// </summary>
        public static WcfIpcProcess Create(IpcHost host, string localAddress)
        {
            try
            {
                var ipc = new WcfIpcProcess(localAddress) { Host = host };
                return ipc;
            }
            catch (AddressAlreadyInUseException e)
            {
                Trace.WriteLine($"Address already in use: {e.Message}", "ERROR");
                throw new AddressAlreadyInUseException($"specified address 'localAddress' is unavailable because it is already in use.");
            }
        }

        public void Abort(Port port)
        {
            throw new NotImplementedException();
        }

        public int AvailableData(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveBuffer.AvailableBytes;
            }
            return -1;
        }

        public void Close(Port port)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Allocates a new flow based on provided flowInformation. This is used by the initiator of the flow request.
        /// </summary>
        /// <param name="flowInformation"></param>
        /// <returns>The Port object that can be used for communication with remote party.</returns>
        public Port Connect(FlowInformation flowInformation)
        {
            lock (m_connectionEndpointsLock)
            {
                var wipcPortname = flowInformation.DestinationAddress.Value.ToString();
                ConnectionEndpoint cep = null;
                if (this.allocateFlow(flowInformation, out cep))
                {
                    var connInfo = new ConnectionInformation()
                    {
                        SourceAddress = LocalAddress.Value.ToString(),
                        DestinationAddress = flowInformation.DestinationAddress.Value.ToString(),
                        SourceProcessName = flowInformation.SourceApplication.ApplicationName,
                        SourceProcessInstance = flowInformation.SourceApplication.ApplicationInstance,
                        SourceEntityName = flowInformation.SourceApplication.EntityName,
                        SourceEntityInstance = flowInformation.SourceApplication.EntityInstance,
                        DestinationProcessName = flowInformation.DestinationApplication.ApplicationName,
                        DestinationProcessInstance = flowInformation.DestinationApplication.ApplicationInstance,
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
                        m_connectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
                        return cep.Port;
                    }
                }
                return null;
            }
        }

        public bool DataAvailable(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return cep.ReceiveBuffer.AvailableBytes != 0;
            }
            return false;
        }

        public Task<bool> DataAvailableAsync(Port port, CancellationToken ct)
        {
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                return Task.Factory.StartNew(() => cep.ReceiveBuffer.WaitForData(ct));
            }
            return Task.Factory.StartNew(() => false);
        }

        public void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            lock (m_generalLock)
            {
                this.m_registeredApplications.RemoveAll(x =>
                {
                    return appInfo.Matches(x.ApplicationInfo);
                });
            }
        }

        public void DeregisterApplication(ApplicationInstanceHandle appInfo, DeregisterApplicationOption option, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public bool Disconnect(Port port, TimeSpan timeout)
        {
            lock (m_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.CommunicationObject.Close();
                    cep.ReceiveBuffer.Dispose();
                    m_connectionEndpoints.Remove(primaryKey: port.Id);
                }
            }
            return true;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            if (port == null) throw new ArgumentNullException("port");
            PortInformationOptions opt = 0;
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                opt |= cep.CommunicationObject.State == CommunicationState.Opened ? PortInformationOptions.Connected : 0;
                opt |= cep.Blocking ? 0 : PortInformationOptions.NonBlocking;
            }
            return opt;
        }
        ApplicationInstanceHandle IRinaIpc.RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            throw new NotImplementedException();
        }

        public byte[] Receive(Port port)
        {
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                var data = cep.ReceiveBuffer;
                if (port.Blocking) data.WaitForData(Timeout.Infinite);
                var buffer = new byte[data.AvailableBytes];
                data.Read(buffer, 0, buffer.Length);
                return buffer;
            }
            return null;
        }
        public int Receive(Port port, byte[] buffer, int offset, int size, PortFlags socketFlags)
        {
            throw new NotImplementedException();
        }

        public void RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            lock (m_generalLock)
            {
                Trace.WriteLine($"Application '{appInfo.ApplicationName}' registered at process {this.LocalAddress}.", "INFO");
                this.m_registeredApplications.Add(new RegisteredApplication() { ApplicationInfo = appInfo, RequestHandler = reqHandler });
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int count)
        {
            ConnectionEndpoint cep;
            if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
            {
                var dataToSent = new byte[count];
                Buffer.BlockCopy(buffer, offset, dataToSent, 0, count);
                var amount = cep.RemoteObject.PushData(cep.ConnectionId, dataToSent);
                return amount;
            }
            return -1;
        }

        public void SetBlocking(Port port, bool value)
        {
            lock (m_connectionEndpointsLock)
            {
                ConnectionEndpoint cep;
                if (m_connectionEndpoints.TryGetValue(primaryKey: port.Id, val: out cep))
                {
                    cep.Blocking = value;
                }
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
            lock (m_connectionEndpointsLock)
            {
                var flowInformation = connInfo.Reverse().GetFlowInformation();
                ConnectionEndpoint cep = null;
                if (allocateFlow(flowInformation, out cep))
                {
                    cep.ConnectionId = cep.Port.Id;
                    m_connectionEndpoints.Add(cep.Port.Id, cep.ConnectionId, cep);
                    return cep.Port;
                }
                return null;
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
            lock (m_generalLock)
            {
                Trace.WriteLine($"Connection request to application '{connectionInformation.DestinationProcessName}'.", "INFO");

                var app = m_registeredApplications.Find(r => { return r.ApplicationInfo.ApplicationName.Equals(connectionInformation.DestinationProcessName, StringComparison.InvariantCultureIgnoreCase); });
                if (app != null)
                {
                    AcceptFlowHandler flowHandler = null;
                    var flowInfo = connectionInformation.Reverse().GetFlowInformation();
                    var reply = ConnectionRequestResult.Reject;
                    try
                    {
                        reply = app.RequestHandler(this, flowInfo, out flowHandler);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"RequestHandler method of {app.ApplicationInfo.ApplicationName}:{e.Message}", "ERROR");
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
                            catch (Exception e) { Trace.WriteLine($"FlowHandler method of {app.ApplicationInfo.ApplicationName}: {e.Message}", "ERROR"); }

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

        protected virtual void Dispose(bool disposing)
        {
            lock (m_disposeLock)
            {
                if (!m_disposedValue)
                {
                    if (disposing)
                    {
                        closeServiceHost();
                        Trace.WriteLine("IPC disposed.", "INFO");
                    }

                    this.m_registeredApplications = null;
                    this.m_connectionEndpoints.Clear();
                    m_disposedValue = true;
                }
            }
        }
        /// <summary>
        /// Private method for allocating a new flow and filling all necessary tables.
        /// </summary>
        /// <param name="flowInstance">An instance of flow for which all the processing is performed.</param>
        /// <param name="remoteObject">The IWinIpcRinaObject object used for remote communication.</param>
        /// <returns>The Port object associated with newly allocated flow.</returns>
        private bool allocateFlow(FlowInformation flowInformation, out ConnectionEndpoint cep)
        {
            var port = new Port(this, m_portidSpace.Next());
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

        private void closeServiceHost()
        {
            if (m_serviceHost.State == CommunicationState.Opened)
            {
                Trace.WriteLine($"Closing IPC service host (State='{this.m_serviceHost.State}').", "INFO");
                foreach (var x in m_connectionEndpoints)
                {
                    var cep = x.Value;
                    try
                    {
                        cep.RemoteObject.CloseConnection(cep.ConnectionId);
                        cep.CommunicationObject.Close();
                    }
                    catch (Exception)
                    {
                        Trace.WriteLine($"Remote site is not available (Address='{cep.FlowInformation.DestinationAddress}', Application='{cep.FlowInformation.DestinationApplication}').", "INFO");
                    }
                }
            }
            m_serviceHost.Close();
        }

        /// <summary>
        /// This class is used to hold properties of a new connection requested by the client side.
        /// Information in this class is used to identify originator of the request as well as to 
        /// find an application that should serve the request.
        /// </summary>
        [DataContract]
        public class ConnectionInformation
        {
            /// <summary>
            /// Represents a destination Windows IPC port name.
            /// </summary>
            [DataMember]
            public string DestinationAddress { get; set; }

            [DataMember]
            public string DestinationEntityInstance { get; set; }

            [DataMember]
            public string DestinationEntityName { get; set; }

            [DataMember]
            public string DestinationProcessInstance { get; set; }

            /// <summary>
            /// Represents a destination application naming information.
            /// </summary>
            [DataMember]
            public string DestinationProcessName { get; set; }

            /// <summary>
            /// Represents a source Windows IPC port name.
            /// </summary>
            [DataMember]
            public string SourceAddress { get; set; }
            [DataMember]
            public string SourceEntityInstance { get; set; }

            [DataMember]
            public string SourceEntityName { get; set; }

            [DataMember]
            public string SourceProcessInstance { get; set; }

            /// <summary>
            /// Represents a source application naming information.
            /// </summary>
            [DataMember]
            public string SourceProcessName { get; set; }
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
        }

        internal class ConnectionEndpoint
        {
            /// <summary>
            /// Specifies whether the port is blocking or non-blocking.
            /// </summary>
            internal bool Blocking;

            /// <summary>
            /// Object used to connect to the remote object.
            /// </summary>
            internal ICommunicationObject CommunicationObject;

            /// <summary>
            /// Connection Id is used to resolve to which connection data should be written as 
            /// remote object is create as a singleton. Connection Id is set to PortId of the "server".
            /// </summary>
            internal ulong ConnectionId;

            /// <summary>
            /// Flow information that describes parameters of the current connection.
            /// </summary>
            internal FlowInformation FlowInformation;

            /// <summary>
            /// Stores the local Port object.
            /// </summary>
            internal Port Port;
            /// <summary>
            /// Local receive buffer. All data that are supposed for this flow are stored in this buffer.
            /// </summary>
            internal FifoStream ReceiveBuffer;

            /// <summary>
            /// Remote object that is used for sending and received data.
            /// </summary>
            internal IRemoteObject RemoteObject;
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
            ChannelFactory<IRemoteObject> _channel;
            WcfIpcProcess _parentObject;
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
                if (_parentObject.m_connectionEndpoints.TryGetValue(subKey: connectionId, val: out cep))
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