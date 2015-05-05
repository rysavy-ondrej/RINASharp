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
            
            internal SouthPortWrapper(Port port)
            {
                Port = port;
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




        bool m_southPortReaderRefreshNeeded = true;
        /// <summary>
        /// Worker just take care of all south ports utilized by the current IPCP. If there are some data avilable then 
        /// these data are sent to the dataflow processing pipeline. 
        /// </summary>
        void Worker()
        {
            // TODO: Check if this does not lead to starvation.      
            try
            {
                Task<bool>[] tasks = null;
                while (true)
                {
                    if (tasks == null || m_southPortReaderRefreshNeeded)
                    {
                        tasks = m_southPortList.Select(x => x.Ipcp.DataAvailableAsync(x.Port)).ToArray();
                        m_southPortReaderRefreshNeeded = false;
                    }
                    int index = Task.WaitAny(tasks);
                    var port = m_southPortList[index].Port;
                    byte[] buffer = port.Ipc.Receive(port);
                    var sdu = new SduInternal(port, buffer, 0, buffer.Length);


                    tasks[index] = m_southPortList[index].Ipcp.DataAvailableAsync(port);
                }
            }
            catch (ThreadAbortException)
            { }
        }



        internal class DataTransfer
        {
            /// <summary>
            /// By processing PDU, DataTransferControl fills missing information in PCI and updates state vector.
            /// </summary>
            /// <param name="p"></param>
            /// <returns></returns>
            internal PduInternal SendPdu(PduInternal p)
            {
                throw new NotImplementedException();
            }
            internal IEnumerable<PduInternal> ReceivePdu(PduInternal p)
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

            internal IEnumerable<PduInternal> Verify(SduInternal p)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Creates a DTP task for newly allocated flow.
        /// </summary>
        async void createDataTransferTask(Port northPort, Port southPort)
        {
            // creating blocks if the processing pipeline
            var delimiter = new SduDelimiter(1000);
            var dataTransfer = new DataTransfer();
            var sduProtection = new SduProtection();

            // linking blocks into southbound pipeline
            var delimiterBlockSbound = new TransformManyBlock<SduInternal, PduInternal>(s => delimiter.Delimite(s));            
            var dataTransferBlockSbound = new TransformBlock<PduInternal, PduInternal>(p => dataTransfer.SendPdu(p));
            var sduProtectionBlockSbound = new TransformBlock<PduInternal, SduInternal>(p => sduProtection.Protect(p));  
                      
            m_northPortMap[northPort.Id].InQueue.LinkTo(delimiterBlockSbound);
            delimiterBlockSbound.LinkTo(dataTransferBlockSbound);
            dataTransferBlockSbound.LinkTo(sduProtectionBlockSbound);
            sduProtectionBlockSbound.LinkTo(new ActionBlock<SduInternal>(s => southPort.Send(s.UserData.Bytes, s.UserData.Offset, s.UserData.Length)));

            // linking blocks into northbound pipeline                                                         
            var sduProtectionBlockNbound = new TransformManyBlock<SduInternal, PduInternal>(p => sduProtection.Verify(p));
            var dataTransferBlockNbound = new TransformManyBlock<PduInternal, PduInternal>(p => dataTransfer.ReceivePdu(p));
            var delimiterBlockNbound = new TransformManyBlock<PduInternal, SduInternal>(s => delimiter.Compose(s));

            var receiver = ReceiveAsync(southPort, sduProtectionBlockNbound);
            sduProtectionBlockNbound.LinkTo(dataTransferBlockNbound);
            dataTransferBlockNbound.LinkTo(delimiterBlockNbound);
            delimiterBlockNbound.LinkTo(m_northPortMap[northPort.Id].OutQueue);

            await receiver;
        }

        static async Task ReceiveAsync(Port southPort, ITargetBlock<SduInternal> target)
        {
            while (await southPort.Ipc.DataAvailableAsync(southPort))
            {
                var buffer = southPort.Ipc.Receive(southPort);
                var sdu = new SduInternal(southPort, buffer, 0, buffer.Length);
                target.Post(sdu);
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

            /// <summary>
            /// This method composes SDUs from provided PDUs. 
            /// </summary>
            /// <param name="pdu"></param>
            /// <returns></returns>
            internal IEnumerable<SduInternal> Compose(PduInternal pdu)
            {
                var sdu = new SduInternal(null, pdu.UserData);
                yield return sdu;
            }

            /// <summary>
            /// This method splits (delimit) sdu into one or more pdus depending on the max pdu size value.
            /// </summary>
            /// <param name="sdu"></param>
            /// <returns></returns>
            internal IEnumerable<PduInternal> Delimite(SduInternal sdu)
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

        public byte[] Receive(Port port)
        {
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                var sdu = pi.InQueue.Receive();
                return sdu.UserData.ActualBytes();
            }
            return null;
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            throw new NotImplementedException();
        }

        public void SetBlocking(Port port, bool value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DataAvailableAsync(Port port)
        {
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                return pi.InQueue.OutputAvailableAsync();
            }
            return null;
        }

        public bool DataAvailable(Port port)
        {
            NorthPortController pi;
            if (m_northPortMap.TryGetValue(port.Id, out pi))
            {
                return pi.InQueue.Count != 0;
            }
            return false;
        }
    }
}

