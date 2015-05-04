//
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
using System.Diagnostics;
using System.Net.Rina.DataUnits;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Linq;
namespace System.Net.Rina
{
    /// <summary>
    /// This is generic IPCProcess class that manages all IPC in the current domain. This is the central class in the 
    /// architecture as it controls all communication in the RINA DIF. It also includes Flow Allocator.
    /// </summary>
    public class IpcProcess : IRinaIpc
    {
        /// <summary>
        /// This internal class maintains information about Ports of supporting IPCPs, e.g.,
        /// Port object and WaitHandle to check if there are data available.
        /// </summary>
        internal class SouthPortWrapper
        {
            internal IRinaIpc Ipcp { get { return this.Port.Ipc; } }
            internal Port Port { get; private set; }
            internal WaitHandle WaitHandle { get; private set; }
            
            internal SouthPortWrapper(Port port, WaitHandle waitHandle)
            {
                Port = port;
                WaitHandle = waitHandle;
            }
        }

        /// <summary>
        /// This class maintains information about provided ports to upper IPCPs.
        /// Each local IPCP port contains two queues. OutQueue stores data available for 
        /// read and InQueue keeps data to be processed by the current IPCP.  
        /// </summary>
        internal class NorthPortController
        {
            internal NorthPortController(Port port)
            {
                Port = port;
            }
            internal Port Port { get; private set; }
            BufferBlock<SduInternal> m_outQueue = new BufferBlock<SduInternal>();
            BufferBlock<SduInternal> m_inQueue = new BufferBlock<SduInternal>();
            internal BufferBlock<SduInternal> OutQueue {  get { return this.m_outQueue;  } }
            internal BufferBlock<SduInternal> InQueue { get { return this.m_inQueue; } }
        }


        IpcConfiguration m_config;
        ResourceInformationManager m_rim;
        Address localAddress;

        Dictionary<ulong, NorthPortController> m_northPortMap = new Dictionary<ulong, NorthPortController>();
        List<SouthPortWrapper> m_southPortList = new List<SouthPortWrapper>();

        private ulong m_lastAllocatedPortId;

        public IpcProcess(IpcConfiguration config, ResourceInformationManager rim)
        {
            m_config = config;
            m_rim = rim;
            localAddress = new Address(AddressFamily.Generic, Text.UTF8Encoding.UTF8.GetBytes(config.LocalAddress));
        }

        /// <summary>
        /// Represents
        /// </summary>
        public Address LocalAddress { get { return this.localAddress; } }


        public FlowState GetFlowState(Port port)
        {
            return FlowState.Open;
        }

        public Port AllocateFlow(FlowInformation flowInfo)
        {
            Address remoteAddress;
            IRinaIpc localipcp;
            if (ResolveFlowAddress(flowInfo, out localipcp, out remoteAddress))
            {
                var flowInfo2 = new FlowInformation()
                {
                    SourceApplication = flowInfo.SourceApplication,
                    DestinationApplication = flowInfo.DestinationApplication,
                    QosParameters = flowInfo.QosParameters,
                    CreateFlowRetriesLimit = flowInfo.CreateFlowRetriesLimit,
                    HopCountLimit = flowInfo.HopCountLimit,
                    Policies = flowInfo.Policies,
                    SourceAddress = localipcp.LocalAddress,
                    DestinationAddress = remoteAddress
                };

                var southPort = localipcp.AllocateFlow(flowInfo2);
                var northPort = new Port(this, ++m_lastAllocatedPortId);
                // TODO: implement linking these two portsthrough DTP and RMT:



            }
            return null;
        }

        /// <summary>
        /// This method is used for translating an address of the current DIF to an address of some of the supporting DIF.
        /// It is like DNS or ARP in the Internet.
        /// </summary>
        /// <param name="flowInfo"></param>
        /// <param name="localipcp"></param>
        /// <param name="remoteAddress"></param>
        /// <returns></returns>
        private bool ResolveFlowAddress(FlowInformation flowInfo, out IRinaIpc localipcp, out Address remoteAddress)
        {
            throw new NotImplementedException();
        }

        WaitHandle[] _southPortWaitHandleArray;
        Port[] _southPortIndexArray;
        void updateWaitHandleList()
        {
            if (_southPortWaitHandleArray == null)
            {
                _southPortWaitHandleArray = m_southPortList.Select(x => x.WaitHandle).ToArray();
                _southPortIndexArray = m_southPortList.Select(x => x.Port).ToArray();
            }
        }

        /// <summary>
        /// Worker just take care of all south ports utilized by the current IPCP. If there are some data avilable then 
        /// these data are sent to the dataflow processing pipeline. 
        /// </summary>
        void Worker()
        {            
            try
            {                       
                while (true)
                {
                    updateWaitHandleList();
                    var waitList = _southPortWaitHandleArray;
                    var portList = _southPortIndexArray;
                    int handleIndex = WaitHandle.WaitAny(waitList);
                    var port = portList[handleIndex];
                    byte[] buffer = new byte[port.Ipc.AvailableData(port)];
                    port.Ipc.Receive(port, buffer, 0, buffer.Length);
                    var sdu = new SduInternal(port, buffer, 0, buffer.Length);

                }
            }
            catch(ThreadAbortException)
            { }
        }



        internal class DataTransfer
        {
            /// <summary>
            /// By processing PDU, DataTransferControl fills missing information in PCI and updates state vector.
            /// </summary>
            /// <param name="p"></param>
            /// <returns></returns>
            internal PduInternal Process(PduInternal p)
            {
                throw new NotImplementedException();
            }
        }

        internal class SduProtection
        {
            internal SduInternal Protect(PduInternal p)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Creates a new DTP task for newly created flow.
        /// </summary>
        void bindDataTransferTask(Port northPort, Port southPort)
        {
            // creating blocks if the processing pipeline
            var delimiter = new SduDelimiter(1000);
            var delimiterBlock = new TransformManyBlock<SduInternal, PduInternal>(s => delimiter.Delimiting(s));

            var dtransfer = new DataTransfer();
            var dtransferBlock = new TransformBlock<PduInternal, PduInternal>(p => dtransfer.Process(p));

            // no RMT here, because this IPCP is just a transit
            var sduprotection = new SduProtection();
            var sduprotectionBlock = new TransformBlock<PduInternal, SduInternal>(p => sduprotection.Protect(p));

            // linking blocks into southbound pipeline
            m_northPortMap[northPort.Id].InQueue.LinkTo(delimiterBlock);
            delimiterBlock.LinkTo(dtransferBlock);
            dtransferBlock.LinkTo(sduprotectionBlock);
            sduprotectionBlock.LinkTo(new ActionBlock<SduInternal>(s => southPort.Send(s.UserData.Bytes, s.UserData.Offset, s.UserData.Length)));

            // linking blocks into northbound pipeline                                                         
            // TODO: implement linking blocks into northbound pipeline       
        }

        /// <summary>
        /// This is an implementatio of RMT functions.
        /// It checks PDU and delivers it to appropriate target.
        /// </summary>
        internal class RelayAndMultiplexNorthBlock : ITargetBlock<PduInternal>
        {
            public Task Completion
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public void Complete()
            {
                throw new NotImplementedException();
            }

            public void Fault(Exception exception)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Offers a message to the ITargetBlock<TInput>, giving the target the opportunity to consume or postpone the message.
            /// </summary>
            /// <param name="messageHeader">A DataflowMessageHeader instance that represents the header of the message being offered.</param>
            /// <param name="messageValue">The value of the message being offered.</param>
            /// <param name="source">The ISourceBlock<TOutput> offering the message. This may be null.</param>
            /// <param name="consumeToAccept">Set to true to instruct the target to call ConsumeMessage synchronously during the call to OfferMessage, 
            /// prior to returning Accepted, in order to consume the message.</param>
            /// <returns>The status of the offered message. If the message was accepted by the target, 
            /// Accepted is returned, and the source should no longer use the offered message, because it is now owned by the target.</returns>
            public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, PduInternal messageValue, ISourceBlock<PduInternal> source, bool consumeToAccept)
            {
                if (consumeToAccept)
                {
                    bool messageConsumed;
                    source.ConsumeMessage(messageHeader, this, out messageConsumed);
                }

                

                return DataflowMessageStatus.Accepted;
            }
        }

        internal class SduDelimiter
        {
            uint m_sequenceNumber;
            int m_maxPduSize;
            internal SduDelimiter(int maxPduSize)
            {
                m_maxPduSize = maxPduSize;                    
            }
            internal IEnumerable<PduInternal> Delimiting(SduInternal sdu)
            {
                var sduSize = sdu.UserData.Length;
                var leftBytes = sduSize;
                for (int i = 0; i < sduSize / m_maxPduSize; i++)
                {
                    var pdu = new PduInternal()
                    {
                        Version = 1,
                        SourceAddress = null,
                        DestinationAddress = null,
                        ConnectionId = null,
                        Flags = 0,
                        PduType = PduType.Dtp,
                        SequenceNumber = ++m_sequenceNumber,
                    };

                    pdu.SetData(sdu.UserData.Bytes, i * m_maxPduSize, Math.Min(m_maxPduSize, leftBytes) );
                    leftBytes -= m_maxPduSize;
                    yield return pdu;
                }
            }
        }


        public void DeallocateFlow (Port port)
		{
			throw new NotImplementedException ();
		}

		public void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
		{
			this.m_rim.SetValue (ResourceClass.ApplicationNames, appInfo.ProcessName, appInfo.EntityName, this.LocalAddress);
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
            // This API call delivers SDUs to Delimiting to delimit into some number of User-Data fields for the DTP task (see above).
            // As noted in the previous section, some OSs may assign semantics to reading a zero-length SDU. 
            // This implies that the API must allow a zero-length SDU and not “optimize” it out of existence. 
            var sdudata = new SduInternal(port, buffer, offset, size);
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                pi.InQueue.Post(sdudata);
                return size;
            }
            return -1;
        }

        public int Receive(Port port, byte[] buffer, int offset, int size)
        {
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                var sdu = pi.InQueue.Receive();
                var count = Math.Min(size, sdu.UserData.Length);
                Buffer.BlockCopy(sdu.UserData.Bytes, sdu.UserData.Offset, buffer, offset, count);
                return count;
            }
            return -1;
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
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                if (pi.InQueue.Count > 0)
                {
                    return 3333;
                }
            }
            return -1;
        }

        public WaitHandle DataAvailableWaitHandle(Port port)
        {
            NorthPortController pi;
            return null;
        }
    }
}

