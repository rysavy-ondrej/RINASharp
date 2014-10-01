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
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
namespace System.Net.Rina
{

	public enum FlowState { Closed, Open }

	/// <summary>
	/// Represents a sinle data block to be send or receive. DataBlock consists of
	/// data and meta information. 
	/// </summary>
	public class DataBlock
	{
		public byte[] Data;
		/// <summary>
		/// The flush flag. If this flag is set then data should be send immediatelly 
		/// and not wait for more data.
		/// </summary>
		public bool Flush;
	}

	/// <summary>
	/// This represents a structure in RINA that keeps information about a single flow. 
	/// </summary>
	public class FlowInstance
	{
		public FlowInformation Info { get; private set; }
		/// <summary>
		/// Port id allocated by Ipc for the current flow instance.
		/// </summary>
		/// <value>The source port identifier.</value>
		public UInt64 SourcePortId 
		{ 
			get { return this.UpflowPort.Id; }
		}


		public Port UpflowPort { get; private set; }
		public Port DownflowPort { get; private set; }
		public int FlowId { get; private set;}

		public ConnectionId[] ConnectionsIds { get; set; }
		public ConnectionId CurrentConnection { get; set; }

		public Task UpflowTask { get; private set; }
		public Task DownflowTask { get; private set; }

		internal FlowInstance (FlowInformation flowInfo, int flowId, Port upflowPort, Port downflowPort, Task upflowTask, Task downflowTask)
		{
			this.Info = flowInfo;
			this.FlowId = flowId;
			this.UpflowPort = upflowPort;
			this.DownflowPort = downflowPort;
			this.UpflowTask = upflowTask;
			this.DownflowTask = downflowTask;
		}

	}

}

