using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Rina.DataUnits;
using System.Text;
using System.Threading.Tasks;
using Systen.Net.Rina.Internals;

namespace System.Net.Rina.Transport
{
    enum DtpState { } 
    class DtStateVector
    {
        int MaxFlowSDUSize;
        int MaxFlowPDUSize;
        int SequenceNumberRollOverThreshold;
        DtpState State;
        bool DTCPpresent;
        bool WindowBased;
        bool RateBased;
        bool RetransmissionPresent;
        bool ClosedWindow;
        bool RateFulfilled;
        int ClosedWindowQueueLength;
        int MaxClosedWindowQueueLen;
        bool PartialDelivery;       /* Partial Delivery of SDUs is Allowed */
        bool IncompleteDelivery;    /* Delivery of Incomplete SDUs is Allowed */
        List<PduInternal> RetransmissionQueue;
        List<PduInternal> ClosedWindowQueue;
        ulong RcvLeftWindowEdge;
        ulong MaxSequenceNumberRcvd;
        ulong SenderLeftWindowEdge;
        ulong NextSequenceNumberToSend;
        List<PduInternal> PDUReassemblyQueue; // List of { PDUs, SeqNum }
        List<ByteArraySegment> User_DataQueue; // List of { User-Data } /* User-Data fields created by Delimiting */
    }
}
