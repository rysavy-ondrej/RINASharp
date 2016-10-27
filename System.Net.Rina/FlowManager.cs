//
//  FlowManager.cs
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
using System.Linq;
using System.Collections.Generic;

namespace System.Net.Rina
{
    /// <summary>
    /// Flow manager represents a flow allocator in RINA architecture. It creates and destroys flow instances.
    /// </summary>
    public class FlowManager
    {
        /// <summary>
        /// Maps flow ids to flows.
        /// </summary>
        Dictionary<ulong, FlowInstance> m_flows = new Dictionary<ulong, FlowInstance>();

        /// <summary>
        /// Creates a new instance of flow manager.
        /// </summary>
        public FlowManager ()
		{
		}

        ulong _lastFlowId = 0;
		public FlowInstance AddFlow (FlowInformation flowInformation)
		{
            lock(this.m_flows)
            {
                _lastFlowId++;
                var flowInstance = new FlowInstance(flowInformation, _lastFlowId);
                this.m_flows.Add(flowInstance.Id, flowInstance);

                return flowInstance;
            }
		}

        /// <summary>
        /// Removes a flow instance from the current flow manager with the given flowId if exists; otherwise it does nothing. 
        /// </summary>
        /// <param name="flowId">An identification of the FlowInstance object.</param>
        public void DeleteFlow(ulong flowId)
        {
            lock(this.m_flows)
            {
                m_flows.Remove(flowId);
            }
        }

        /// <summary>
        /// Gets flow of the given flow id.
        /// </summary>
        /// <param name="fid">An id of the flow to be retrieved.</param>
        /// <returns>A flow instance object.</returns>
        /// <exception cref="KeyNotFoundException">If flow with the given id does not exist.</exception>
        public FlowInstance GetFlowInstance(ulong fid)
        {   
            lock (this.m_flows)
            {
                return this.m_flows[fid];
            }
        }

        /// <summary>
        /// Selects the flows by evaluating the specified selector function.
        /// </summary>
        /// <param name="selector">Selector function that specifies criteria for flowinstance object to be included in the result.</param>
        /// <returns>An array of flow instances that matches specified criteria.</returns>
        FlowInstance[] SelectFlows(Func<FlowInstance, bool> selector)
		{
            lock (this.m_flows)
            {
                var flows = m_flows.Values.Where(selector);
                return flows.ToArray();
            }
		}
	}
}

