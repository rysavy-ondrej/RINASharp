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
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Security.Permissions;
using System.Net.Rina;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization;
namespace System.Net.Rina.Shims
{
    /// <summary>
    /// This class is used to hold propwerties of a new connection requested by the client side.
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
    /// This is implementation of ShimDif that employs Windows IPC mechanism.
    /// </summary>
    /// <remarks>
    /// The System.Runtime.Remoting.Channels.Ipc namespace defines a communication channel for remoting that uses the interprocess cmmunication (IPC) 
    /// system of the Windows operating system. Because it does not use network communication, the IPC channel is much faster than the HTTP and TCP channels, but it can only be used for communication between application domains on the same physical computer.
    /// See https://msdn.microsoft.com/en-us/library/system.runtime.remoting.channels.ipc.ipcchannel(v=vs.110).aspx for details on IpcChannel.
    /// 
    /// This IPC is a part of IPC DIF that provides reliable data delivery. It uses recievue buffers only as it is possible to 
    /// use send and forgot method. 
    /// </remarks>
    [ShimIpc("WinIpc")]
    public class WinIpcObject : IpcContext, IDisposable
	{
        Object _flowManipulationLock = new Object();
        Object _appManipulationLock = new Object();
        ulong _lastPortId = 0;
        Thread _worker;
        FlowManager _flowManager = new FlowManager();
        ServiceHost _serviceHost;

        internal class RemoteSite
        {
            internal RemoteSite(ICommunicationObject comObject, IRemoteObject remoteObject)
            {
                this.CommunicationObject = comObject;
                this.RemoteObject = remoteObject;
            }
            internal ICommunicationObject CommunicationObject { get; private set; }
            internal IRemoteObject RemoteObject { get; private set; }

            internal void Close()
            {
                this.CommunicationObject.Close();
            }
        }

        /// <summary>
        /// Maps Port Ids to Flow Ids.
        /// </summary>
        Dictionary<ulong, ulong> _portIdsToFlowIds = new Dictionary<ulong, ulong>();
        /// <summary>
        /// Maps Flow Ids to remote objects.
        /// </summary>
        Dictionary<ulong, RemoteSite> _flowIdsToRemoteObjects = new Dictionary<ulong, RemoteSite>();
        /// <summary>
        /// Maps remote port ids (aka connection ids) to flow ids. 
        /// </summary>
        Dictionary<ulong, ulong> _remotePortIdsToFlowIds = new Dictionary<ulong, ulong>();
        /// <summary>
        /// Collection all registered applications and their request handlers.
        /// </summary>
        List<RegisteredApplication> _registeredApplications = new List<RegisteredApplication>();
        /// <summary>
        /// Maps flow ids to receive buffer objects.
        /// </summary>
        Dictionary<ulong, Internals.FifoStream> _receiveBuffers = new Dictionary<ulong, Internals.FifoStream> ();

        public Address LocalAddress { get; private set; }

        /// <summary>
        /// Provides access to working thread. 
        /// </summary>
        public Thread Worker {  get { return this._worker; } }

        public static WinIpcObject Create(string localAddress)
        {
            try
            {
                var ipc = new WinIpcObject(localAddress);
                ipc._worker = new Thread(ipc.Run);
                ipc._worker.Start();
                return ipc;
            }
            catch(AddressAlreadyInUseException e)
            {
                var info = String.Format("Address already in use: {0}", e.Message);
                Trace.WriteLine(info, "ERROR");
                return null;
            }
        }

        internal FlowInstance GetFlowByConnectionId(ulong connectionId)
        {
            lock(_flowManipulationLock)
            {
                return this._flowManager.GetFlowInstance(this._remotePortIdsToFlowIds[connectionId]);
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
                    var info = String.Format("{0}: Flow count {1}", DateTime.Now,_receiveBuffers.Count);
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
        WinIpcObject(string localAddress)
        {
            LocalAddress = Address.FromWinIpcPort(localAddress);
            // create server side of channel...
            _serviceHost = new ServiceHost(new RemoteObject(this), new Uri[]{new Uri((string)this.LocalAddress.Value)});
            _serviceHost.AddServiceEndpoint(typeof(IRemoteObject), new NetNamedPipeBinding(), "WinIpcObject");
            _serviceHost.Open();                        
        }

        /// <summary>
        /// Private method for allocating a new flow and filling all necessary tables.
        /// </summary>
        /// <param name="flowInstance">An instance of flow for which all the processing is perfomed.</param>
        /// <param name="remoteObject">The IWinIpcRinaObject object used for remote communication.</param>
        /// <returns>The Port object associated with newly allocated flow.</returns>
        private Port allocateFlow(FlowInstance flowInstance, out IRemoteObject remoteObject)
        {
            var port = new Port(this, ++_lastPortId);
            _portIdsToFlowIds.Add(port.Id, flowInstance.Id);

            var localAddress = (string)this.LocalAddress.Value;
            var remoteAddres = (string)flowInstance.Information.DestinationAddress.Value;
            // connect to the target IPC:
            ChannelFactory<IRemoteObject> channelFactory = new ChannelFactory<IRemoteObject>(new NetNamedPipeBinding());
            remoteObject = channelFactory.CreateChannel(new EndpointAddress(string.Format("{0}/WinIpcObject", remoteAddres)));                        
            _flowIdsToRemoteObjects.Add(flowInstance.Id, new RemoteSite(channelFactory,remoteObject));
            _receiveBuffers.Add(flowInstance.Id, new Internals.FifoStream(8192));
            return port;
        }
            /// <summary>
            /// Allocates a new flow based on provided flowInformation.
            /// </summary>
            /// <param name="flowInformation"></param>
            /// <returns>The Port object that can be used for communication with remote party.</returns>
        public Port AllocateFlow (FlowInformation flowInformation)
		{
            lock(_flowManipulationLock)
            {
                var wipcPortname = (string)flowInformation.DestinationAddress.Value;
                var flowInstance = _flowManager.AddFlow(flowInformation);

                IRemoteObject remoteObject = null;
                var port = this.allocateFlow(flowInstance, out remoteObject);

                var connInfo = new ConnectionInformation()
                {
                    SourceAddress = (string)this.LocalAddress.Value,
                    DestinationAddress = (string)flowInstance.Information.DestinationAddress.Value,
                    SourceProcessName = flowInformation.SourceApplication.ProcessName,
                    SourceProcessInstance = flowInformation.SourceApplication.ProcessInstance,
                    SourceEntityName = flowInformation.SourceApplication.EntityName,
                    SourceEntityInstance = flowInformation.SourceApplication.EntityInstance,
                    DestinationProcessName = flowInformation.DestinationApplication.ProcessName,
                    DestinationProcessInstance = flowInformation.DestinationApplication.ProcessInstance,
                    DestinationEntityName = flowInformation.DestinationApplication.EntityName,
                    DestinationEntityInstance = flowInformation.DestinationApplication.EntityInstance
                };
                flowInstance.RemotePortId = remoteObject.OpenConnection(connInfo);
                _remotePortIdsToFlowIds[flowInstance.RemotePortId] = flowInstance.Id;
                return port;
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
            lock(_flowManipulationLock)
            {
                var flowInstance = _flowManager.AddFlow(connInfo.Reverse().GetFlowInformation());
                IRemoteObject remoteObject = null;
                var port = allocateFlow(flowInstance, out remoteObject);
                flowInstance.RemotePortId = port.Id;
                _remotePortIdsToFlowIds[flowInstance.RemotePortId] = flowInstance.Id;
                return port;
            }
        }

		public void DeallocateFlow (Port port)
		{
            lock(_flowManipulationLock)
            {
                var fid = _portIdsToFlowIds[port.Id];
                var flow = _flowManager.GetFlowInstance(fid);

                _receiveBuffers.Remove(fid);
                _flowIdsToRemoteObjects[fid].Close();
                _flowIdsToRemoteObjects.Remove(fid);
                _remotePortIdsToFlowIds.Remove(flow.RemotePortId);
                _portIdsToFlowIds.Remove(port.Id);
                _flowManager.DeleteFlow(fid);
            }
        }

        public int Send(Port port, byte[] buffer, int offset, int count)
        {
            var fid = _portIdsToFlowIds[port.Id];
            var flowInst = _flowManager.GetFlowInstance(fid);
            var remote = _flowIdsToRemoteObjects[fid];

            var dataToSent = new byte[count];
            Buffer.BlockCopy(buffer, offset, dataToSent, 0, count);

            var amount = remote.RemoteObject.PushData(flowInst.RemotePortId, dataToSent);
            return amount;
        }

		public int Receive (Port port, byte[] buffer, int offset, int count)
		{
            var fid = _portIdsToFlowIds[port.Id];
            var data = _receiveBuffers[fid];
            if (port.Blocking) data.WaitForData(Timeout.Infinite);
            var bytesToRead = Math.Min(data.AvailableBytes, count);
            return data.Read(buffer, offset, bytesToRead);
		}
        

		public void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
		{
            lock(_appManipulationLock)
            {
                var info = String.Format("Application '{0}' registered.", appInfo.ProcessName);
                Trace.WriteLine(info, "INFO");
                this._registeredApplications.Add(new RegisteredApplication() { ApplicationInfo = appInfo, Handler = reqHandler });
            }
		}

        public void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            lock(_appManipulationLock)
            {
                this._registeredApplications.RemoveAll(x =>
                {
                    return appInfo.Matches(x.ApplicationInfo);
                });
            }
        }

        internal ulong ConnectToApplication(ConnectionInformation connectionInformation)
        {
            lock(_appManipulationLock)
            {
                var info = String.Format("Connection request to application '{0}'.", connectionInformation.DestinationProcessName);
                Trace.WriteLine(info, "INFO");

                var app = _registeredApplications.Find(r => { return r.ApplicationInfo.ProcessName.Equals(connectionInformation.DestinationProcessName, StringComparison.InvariantCultureIgnoreCase); });
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

        public FlowState GetFlowState(Port port)
        {
            throw new NotImplementedException();
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
                        var info = String.Format("Shutting down worker thread '{0}'.", this._worker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        this._worker.Abort();

                        info = String.Format("Waiting for thread '{0}' to finish.", this._worker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        this._worker.Join();

                        info = String.Format("Thread '{0}' finished. Disposing managed resources of the current IPC.", this._worker.ManagedThreadId);
                        Trace.WriteLine(info, "INFO");

                        CloseServiceHost();

                        info = String.Format("IPC disposed.");
                        Trace.WriteLine(info, "INFO");
                    }
                    this._registeredApplications = null;
                    this._receiveBuffers.Clear();
                    this._portIdsToFlowIds.Clear();
                    this._flowManager = null;
                    this._flowIdsToRemoteObjects.Clear();
                    this._remotePortIdsToFlowIds.Clear();
                    _disposedValue = true;
                }
            }
        }
        
        private void CloseServiceHost()
        {
            if (_serviceHost.State == CommunicationState.Opened)
            {
                var info = String.Format("Closing IPC service host (State='{0}').", this._serviceHost.State);
                Trace.WriteLine(info, "INFO");
                foreach (var x in _flowIdsToRemoteObjects)
                {
                    var f = this._flowManager.GetFlowInstance(x.Key);
                    try { x.Value.RemoteObject.CloseConnection(f.RemotePortId); x.Value.Close(); } catch (Exception)
                    {
                        info = String.Format("Remote site is not available (Address='{0}', Application='{1}').", f.Information.DestinationAddress, f.Information.DestinationApplication, f.RemotePortId);
                        Trace.WriteLine(info, "INFO");
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
            WinIpcObject _parentObject;
            ChannelFactory<IRemoteObject> _channel;
            public RemoteObject(WinIpcObject parent)
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

                var flowInst = _parentObject.GetFlowByConnectionId(connectionId);
                var recvBuffer = _parentObject._receiveBuffers[flowInst.Id];
                recvBuffer.Write(data, 0, data.Length);

                info = String.Format("Receiving buffer now contains {0}bytes.", recvBuffer.AvailableBytes);
                Trace.WriteLine(info, "INFO");
                return data.Length;
            }
        }
        #endregion
    }
}

