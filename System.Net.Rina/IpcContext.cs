//
//  IPC.cs
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
namespace System.Net.Rina
{
	/// <summary>
	/// This delegate represent a method that is called for each new request represented by the passed flow object.
	/// </summary>
	public delegate void RequestHandler(FlowInstance flow);

	/// <summary>
	/// This interface represents basic IPC API.
	/// </summary>
	public abstract class IpcContext {

		public abstract Address LocalAddress { get; }


		/// <summary>
		/// Allocates the new flow according the specified information.
		/// </summary>
		/// <returns>The flow.</returns>
		/// <param name="flow">Flow information object.</param>
		public abstract Port AllocateFlow (FlowInformation flow);
		/// <summary>
		/// Deallocates the flow associated with the provided port.
		/// </summary>
		/// <param name="port">Port descriptor.</param>
		public abstract void DeallocateFlow (Port port);


		public abstract FlowState GetFlowState (Port port);

		
		/// <summary>
		/// Sends the specified data using given port.
		/// </summary>
		/// <param name="port">Port.</param>
		/// <param name="data">Data.</param>
		public abstract void Send(Port port, byte[] data);

		public async Task SendAsync(Port port, byte[] data)
		{
			var t = new Task(() => { this.Send (port, data); });
			await t;
		}
		/// <summary>
		/// Reads the available data from the specified port.
		/// </summary>
		/// <param name="port">Port.</param>
		public abstract byte[] Receive(Port port);

		public async Task<byte[]> ReceiveAsync(Port port)
		{
			var t = new Task<byte[]> (() => {
				return this.Receive (port);
			});
			return await t;
		}

		/// <summary>
		/// Registers the application name in the current IPC. An application serves new flows that are passed
		/// to the application using provided RequestHandler delegate.
		/// </summary>
		/// <param name="appInfo">App info.</param>
		/// <param name="reqHandler">Req handler.</param>
		public abstract void RegisterApplication (ApplicationNamingInfo appInfo, RequestHandler reqHandler);
		/// <summary>
		/// Deregisters the application.
		/// </summary>
		/// <param name="appInfo">App info.</param>
		public abstract void DeregisterApplication (ApplicationNamingInfo appInfo);
	}

}

