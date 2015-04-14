//
//  HttpShimDif.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE Consortium (http://ict-pristine.eu)
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
using System.Net.Rina;
namespace System.Net.Rina.Shims
{
	/// <summary>
	/// This class implement a simple Http shim dif. This DIF uses HTTP protocol for transfering data 
	/// and DNS system for naming. Routing is simply IP routing.
	/// </summary>
	/// <remarks>
	/// Each DIF implement IPCContext interface providing data transfer and addressing functions for upper DIFs. 
	/// Then DIF should run management, routing, naming and data transfer AE.
	/// </remarks>
	[ShimIpc("Http")]
    public class HttpIpcContext : IpcContext
	{          

		#region IpcContext implementation
		public override Port AllocateFlow (FlowInformation flow)
		{
			throw new NotImplementedException ();
		}
		public override void DeallocateFlow (Port port)
		{
			throw new NotImplementedException ();
		}
		public override void Send (Port port, byte[] data)
		{
			throw new NotImplementedException ();
		}
		public override byte[] Receive (Port port)
		{
			throw new NotImplementedException ();
		}
		public override void RegisterApplication (ApplicationNamingInfo appInfo, RequestHandler reqHandler)
		{
			throw new NotImplementedException ();
		}
		public override void DeregisterApplication (ApplicationNamingInfo appInfo)
		{
			throw new NotImplementedException ();
		}
		#endregion
	}
}

