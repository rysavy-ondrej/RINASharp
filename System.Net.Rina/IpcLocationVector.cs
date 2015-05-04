//
//  Location.cs
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
	/// Represents end point in Ipc communication. As an end point we need to know address of the 
	/// node in the given ipc.
	/// </summary>
	public class IpcLocationVector
	{
		public Address RemoteAddress { get; private set; }
		public IRinaIpc LocalIpc { get; private set; }

		public IpcLocationVector(Address address, IRinaIpc ipc)
		{
			this.RemoteAddress = address;
			this.LocalIpc = ipc;
		}

		public readonly static IpcLocationVector None = new IpcLocationVector(new Address(IPAddress.None), null);
	}
}

