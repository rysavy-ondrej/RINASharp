//
//  IpcManager.cs
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
using System.Collections.Generic;
namespace System.Net.Rina
{
	/// <summary>
	/// This is IPC manager class that manages all IPC in the current domain. This is the central class in the 
	/// architewcture as it controls all communication in the RINA DIF. It also includes Flow Allocator.
	/// </summary>
	public class IpcManager : IIpc
	{
		IpcConfiguration _config;
		ResourceInformationManager _rim;
		FlowManager _fam;

		/// <summary>
		/// Represents
		/// </summary>
		Address Address { get; private set; }


		public IpcManager(IpcConfiguration config, ResourceInformationManager rim, FlowManager fam)
		{
			this._config = config;
			this._rim = rim;
			this._fam = fam;
		}

		/// <summary>
		/// List of all active flows currently allocated in the current DIF.
		/// </summary>
		Dictionary<Flow, FlowInstance> activeFlows = new Dictionary<Flow, FlowInstance>();
		Dictionary<Port, FlowInstance> portToFlow = new Dictionary<Port, FlowInstance>();

		#region IIpc implementation
		public Port AllocateFlow (Flow flow)
		{
			// find destination application using RIB:
			this._rim.GetValue ("ApplicationProcessNames", flow.DestinationApplication.ProcessName, null);
			throw new NotImplementedException ();
		}
		public void DeallocateFlow (Port port)
		{
			throw new NotImplementedException ();
		}
		public void Send (Port port, byte[] data)
		{
			throw new NotImplementedException ();
		}
		public byte[] Receive (Port port)
		{
			throw new NotImplementedException ();
		}
		public void RegisterApplication (ApplicationNamingInfo appInfo, RequestHandler reqHandler)
		{

			this._rim.SetValue (ResourceClass.ApplicationNames, appInfo.ProcessName, appInfo.EntityName, this.Address);

		}
		public void DeregisterApplication (ApplicationNamingInfo appInfo)
		{
			throw new NotImplementedException ();
		}
		#endregion
	}
}

