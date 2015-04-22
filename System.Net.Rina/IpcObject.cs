﻿//
//  IpcManager.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE
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
using System.Threading.Tasks;
namespace System.Net.Rina
{
	/// <summary>
	/// This is IPC manager class that manages all IPC in the current domain. This is the central class in the 
	/// architecture as it controls all communication in the RINA DIF. It also includes Flow Allocator.
	/// </summary>
	public class IpcObject : IpcContext
	{
		IpcConfiguration _config;
		ResourceInformationManager _rim;
		FlowManager _fam;
		List<IpcContext> _difs;
		UInt64 portId =0;
		Address localAddress;

	    /// <summary>
        /// Represents a collection of input buffers associated with ports.
        /// </summary>
		private Dictionary<UInt64,BytesBlockBuffer> _readBuffers = new Dictionary<ulong, BytesBlockBuffer>();
        private Dictionary<UInt64, BytesBlockBuffer> _writeBuffers = new Dictionary<ulong, BytesBlockBuffer>();

        public IpcObject(IpcConfiguration config, ResourceInformationManager rim, FlowManager fam, IpcContext[] underlayingIpcs)
        {
            this._config = config;
            this._rim = rim;
            this._fam = fam;
            this._difs = new List<IpcContext>(underlayingIpcs);
            this.localAddress = new Address(AddressFamily.Generic, new byte[0]);
        }

        /// <summary>
        /// Represents
        /// </summary>
        public Address LocalAddress { get { return this.localAddress; } }



        /// <summary>
        /// Enumerates all available IPC context in the system. Usually, all IPCs for Shim DIFs are returned.
        /// </summary>
        /// <returns>An enumeration of IPC context defined in the system.</returns>
        public static IEnumerable<IpcContext> GetAvailableContexts()
        {
            return null;    
        }

		public FlowState GetFlowState (Port port)
		{
			return FlowState.Open;
		}

		public Port AllocateFlow (FlowInformation flowInfo)
		{
			// find destination application using local RIB, it yields underlying DIF and port:
			var locvector = (IpcLocationVector)(this._rim.GetValue(ResourceClass.ApplicationNames, 
				flowInfo.DestinationApplication.ProcessName, 
				flowInfo.DestinationApplication.EntityName, 
				IpcLocationVector.None));

			var upflowPort = new Port (this, this.portId++); 

			// this is flow info passed to underlaying dif in order to allocate flow there...
			var underlayingFlowInfo = new FlowInformation () {
				SourceApplication = flowInfo.SourceApplication,
				DestinationApplication = flowInfo.DestinationApplication,
				SourceAddress = locvector.LocalIpc.LocalAddress,
				DestinationAddress = locvector.RemoteAddress,
				CreateFlowRetriesLimit = 3,
				HopCountLimit = 64,
				Policies = default(IpcPolicies),
				QosParameters = default(QosParameters)			
			};
			var downflowPort = locvector.LocalIpc.AllocateFlow (underlayingFlowInfo);					

			// Processing pipeline for upflow direction:
			//      o upflow_sinkPort -> upflowPort.Receive  	(Consumer)
			//      |
			//	   [ ] upflow_internalBuffer					(Buffering)
			//		|
			//     < > upflow_unpackData						(Transform)
			//      |
			//      o upflow_recPort <- downflowPort   			(Producer)

			var upflow_sinkPort = new SinkPort ((byte[] bytes) => {
				this._readBuffers[upflowPort.Id].Enqueue(bytes);
			});

			var upflow_internalBuffer = new System.Threading.Tasks.Dataflow.BufferBlock<byte[]> (); 

			var upflow_unpackData = new System.Threading.Tasks.Dataflow.TransformBlock<byte[], byte[]> (bytes => {
					DataTransferProtocol dtp = new DataTransferProtocol (bytes);
					return dtp.GetPayload ();
				});
				
			var upflow_recPort = new ReceivingPort (downflowPort); 

			upflow_recPort.Produce (upflow_unpackData);

			upflow_unpackData.LinkTo (upflow_internalBuffer, new System.Threading.Tasks.Dataflow.DataflowLinkOptions() { PropagateCompletion = true });

			var upflow_task = upflow_sinkPort.ConsumeAsync (upflow_internalBuffer);

			this._fam.AddFlow (flowInfo);
			// return entry point...
			return upflowPort;
		}

		public void DeallocateFlow (Port port)
		{
			throw new NotImplementedException ();
		}

		public void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
		{
			this._rim.SetValue (ResourceClass.ApplicationNames, appInfo.ProcessName, appInfo.EntityName, this.LocalAddress);
		}

		public void DeregisterApplication (ApplicationNamingInfo appInfo)
		{
			throw new NotImplementedException ();
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

        public int Send(Port port, byte[] buffer, int offset, int size)
        {
            throw new NotImplementedException();
        }

        public int Receive(Port port, byte[] buffer, int offset, int size)
        {
            throw new NotImplementedException();
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            throw new NotImplementedException();
        }

        public void SetBlocking(Port port, bool value)
        {
            throw new NotImplementedException();
        }

        public int AvailableData(Port port)
        {
            throw new NotImplementedException();
        }
    }
}
