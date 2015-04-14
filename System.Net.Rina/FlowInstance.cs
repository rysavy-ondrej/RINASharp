//
//  FlowInstance.cs
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
using System.IO;
namespace System.Net.Rina
{

    public enum FlowState { Closed, Open }

    /// <summary>
    /// This represents a structure in RINA that keeps operational information about a single flow.
    /// </summary>
    public class FlowInstance
    {
        /// <summary>
        /// Gets or sets a value representing the remote port id for this FlowInstance.
        /// </summary>
        public ulong RemotePortId { get; set; }

        /// <summary>
        /// Gets FlowInformation for this FlowInstance.
        /// </summary>
        public FlowInformation Information { get; private set; }

        /// <summary>
        /// Gets flow id.
        /// </summary>
        public ulong Id { get; private set;}
        
        /// <summary>
        /// Creates a new FlowInstance using specified flow information and flow identifier.
        /// </summary>
        /// <param name="flowInfo"></param>
        /// <param name="flowId"></param>
		internal FlowInstance (FlowInformation flowInfo, ulong flowId)
		{
            Information = flowInfo;
            Id = flowId;
		}
	}
}

