//
//  QoSParameters.cs
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

namespace System.Net.Rina
{
    /// <summary>
    /// Specifies the level of service to negotiate for the flow.
    /// </summary>
    public enum ServiceType
    {
        /// <summary>
        /// Used only for transmission of control packets. This ServiceType has the highest priority.
        /// </summary>
        NetworkControl,

        /// <summary>
        /// Guarantees that datagrams will arrive within the guaranteed delivery time and will not be
        /// discarded due to queue overflows, provided the flow's traffic stays within its specified
        /// traffic parameters.
        /// </summary>
        /// <remarks>
        /// Guarantees that datagrams will arrive within the guaranteed delivery time and will not be
        /// discarded due to queue overflows, provided the flow's traffic stays within its specified
        /// traffic parameters. This service is intended for applications that need a firm guarantee
        /// that a datagram will arrive no later than a certain time after it was transmitted by its source.
        /// </remarks>
        Guarenteed,

        /// <summary>
        /// Provides an end-to-end QOS that closely approximates transmission quality provided by
        /// best-effort service, as expected under unloaded conditions from the associated network
        /// components along the data path.
        /// </summary>
        /// <remarks>
        /// Applications that use SERVICETYPE_CONTROLLEDLOAD may therefore assume the following: (i)
        /// The network will deliver a very high percentage of transmitted packets to its intended
        /// receivers. In other words, packet loss will closely approximate the basic packet error
        /// rate of the transmission medium. (ii) Transmission delay for a very high percentage of
        /// the delivered packets will not greatly exceed the minimum transit delay experienced by
        /// any successfully delivered packet.
        /// </remarks>
        ControlledLoad,

        /// <summary>
        /// Traffic control does create a BESTEFFORT flow.
        /// </summary>
        /// <remarks>
        /// Traffic control does create a BESTEFFORT flow, however, and traffic on the flow will be
        /// handled by traffic control similarly to other BESTEFFORT traffic.
        /// </remarks>
        BestEffort,

        /// <summary>
        /// Indicates that the application requires better than BESTEFFORT transmission, but cannot
        /// quantify its transmission requirements.
        /// </summary>
        /// <remarks>
        /// Applications that use SERVICETYPE_QUALITATIVE can supply an application identifier policy
        /// object. The application identification policy object enables policy servers on the
        /// network to identify the application, and accordingly, assign an appropriate quality of
        /// service to the request. For more information on application identification, consult the
        /// IETF Internet Draft draft-ietf-rap-rsvp-appid-00.txt, or the Microsoft white paper on
        /// Application Identification. Traffic control treats flows of this type with the same
        /// priority as BESTEFFORT traffic on the local computer.
        /// </remarks>
        Qualitative
    }

    /// <summary>
    /// The FLOWSPEC structure provides quality of service parameters to flows. This allows QOS-aware
    /// applications to invoke, modify, or remove QOS settings for a given flow.
    /// </summary>
    /// <remarks>
    /// The intention of <see cref="QosParameters"/> is similar to FLOWSPEC structure of WSA as defined
    /// here: <seealso cref="https://msdn.microsoft.com/en-us/library/windows/desktop/aa373702(v=vs.85).aspx"/>
    /// </remarks>
    public struct QosParameters
    {
        public ulong TokenRate;
        public ulong TokenBucketSize;
        public ulong PeakBandwidth;
        public ulong Latency;
        public ulong DelayVariation;
        public ServiceType ServiceType;
        public ulong MaxSduSize;
        public ulong MinimumPolicedSize;

        public static QosParameters BestEffort
        {
            get
            {
                return new QosParameters()
                {
                    ServiceType = ServiceType.BestEffort
                };
            }
        }
    }
}

