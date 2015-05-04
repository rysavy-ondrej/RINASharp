using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Systen.Net.Rina.Internals;

namespace System.Net.Rina.DataUnits
{
    class ConnectionId
    {
        /// <summary>
        /// A DIF-assigned identifier only known within the DIF that stands for a particular QoS hypercube.
        /// </summary>
        UInt32 QosId;
        /// <summary>
        /// An identifier unambiguous within the system in which the destination-IPC-Process resides that identifies 
        /// the binding between the IPC-Process and an Application-Entity-instance of the Destination-Application-Process
        /// </summary>
        UInt64 DestinationCepId;
        /// <summary>
        /// An identifier unambiguous within the system in which the source-IPC-Process resides that identifies 
        /// the binding between the IPC-Process and an Application-Entity-instance of the Source-Application-Process. 
        /// </summary>
        UInt64 SourceCepId;
    }

    enum PduType { Efcp = 0x8000,
        Dtp = 0x8001,
        Dtcp = 0x8800,
        DtcpControlAck = 0x8803,
        DtcpControlAckOnly = 0x8804, 
        DtcpControlNackOnly = 0x8805, 
        DtcpControlAckFlowControl = 0x880c,
        DtcpControlNackFlowControl = 0x880d,
        DtcpControlOnly = 0x8808, 
        DtcpSelectiveAck = 0x8806,
        DtcpSelectiveNack = 0x8807, 
        DtcpSelectiveAckFlowControl = 0x880e,
        DtcpSelectiveNackFlowControl = 0x880f, 
        Management = 0xc000 }

    internal class PduInternal
    {

        /// <summary>
        /// An identifier indicating the version of the Protocol. 
        /// </summary>
        internal byte Version { get; set; }

        /// <summary>
        /// A synonym for the Application-Process. Name designating the IPCProcess with Scope 
        /// limited to the DIF and a binding to the Destination Application Process.
        /// </summary>
        internal Address DestinationAddress { get; set; }
        /// <summary>
        /// A synonym for the Application-Process. Name designating an IPC-Process with Scope 
        /// limited to the DIF and a binding to the Source Application Process. 
        /// </summary>
        internal Address SourceAddress { get; set; }

        /// <summary>
        ///  A three part identifier unambiguous within Scope of two communicating IPC-Processes used to distinguish connections between them.
        /// </summary>
        internal ConnectionId ConnectionId { get; set; }

        internal PduType PduType { get; set; }

        internal byte Flags { get; set; }

        /// <summary>
        /// The contents of this field is total length of the PDU in bytes. 
        /// </summary>
        internal UInt32 PduLength { get; }

        /// <summary>
        /// The contents of this field is the sequence number of the PDU. 
        /// </summary>
        internal UInt32 SequenceNumber { get; set; }

        /// <summary>
        ///  This field contains one or more octets that are uninterpreted by the EFCP. 
        /// The Delimiting function creates this field containing SDU-Fragments and/or one or more complete-SDUs up to 
        /// the (MaxPDUSize-PCISize). The Delimiting of the User-Data Field is defined by the Delimiting Policy. 
        /// </summary>
        internal ByteArraySegment UserData { get; private set; }

        internal void SetData(byte[] buffer, int offset, int size)
        {
            this.UserData = new ByteArraySegment(buffer, offset, size);
        }

        internal void SetData(ByteArraySegment userData)
        {
            UserData = new ByteArraySegment(userData);
        }
    }



}
