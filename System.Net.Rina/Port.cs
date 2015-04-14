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
	/// <summary>
	/// Represents a port used to identify communication within DIF.
	/// </summary>
	public class Port
	{
		public IpcContext Ipc { get; private set; }
		public UInt64 Id { get; private set; }
        /// <summary>
        /// Gets or sets a value that indicates whether the Port is in blocking mode.
        /// </summary>
        public bool Blocking { get; set; }
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

		internal Port (IpcContext ipc, UInt64 id)
		{
			this.Ipc = ipc;
			this.Id = id;
		}
	}
}

