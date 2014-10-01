//
//  Port.cs
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
	public struct PortInformation
	{

	}
	/// <summary>
	/// Represents a port used to identify communication within DIF.
	/// </summary>
	public class Port
	{
		public IpcContext Ipc { get; private set; }
		public UInt64 Id { get; private set; }
		public PortInformation PortInformation { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this <see cref="System.Net.Rina.Port"/> is connected.
		/// </summary>
		/// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
		public bool Connected 
		{  
			get {
				return this.Ipc.GetFlowState (this) == FlowState.Open;
			}		
		}

		public Port (IpcContext ipc, UInt64 id, PortInformation portInformation)
		{
			this.Ipc = ipc;
			this.Id = id;
			this.PortInformation = portInformation;
		}
	}
}

