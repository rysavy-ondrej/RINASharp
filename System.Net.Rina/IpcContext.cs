//
//  IpcContext.cs
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
    /// The result of connection request.
    /// </summary>
    public enum ConnectionRequestResult { Accept, Reject }

    /// <summary>
    /// This delegate represent a method that is called for each new request.Application request handler
    /// provides flow information that can be used by the application to decide whether to accept or deny the 
    /// request.
    /// </summary>
    /// <param name="context">The IpcContext object which manages the communication.</param>
    /// <param name="flowInformation">The flow information describing the new request.</param>
    /// <param name="acceptFlowHandler">A delegate that will be called in case of Accept response to fed the application with the information about the connection parameters.</param>
    /// <returns>The connection request result. It can be either Accept or Reject.</returns>
    public delegate ConnectionRequestResult ConnectionRequestHandler(IpcContext context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler);

    /// <summary>
    /// This delegate is used to inform application that accepted a new request about the created flow. In particulat, application 
    /// to serve the new request has to know port used for further communication.
    /// </summary>
    /// <param name="context">The IpcContext object which manages the communication.</param>
    /// <param name="flowInformation">The flow information describing communicaton parties.</param>
    /// <param name="port">The port object used to communication with other end point.</param>
    public delegate void AcceptFlowHandler(IpcContext context, FlowInformation flowInformation, Port port);
	/// <summary>
	/// This interface represents basic IPC API.
	/// </summary>
	public interface IpcContext {

		Address LocalAddress { get; }


		/// <summary>
		/// Allocates the new flow according the specified information.
		/// </summary>
		/// <returns>The flow.</returns>
		/// <param name="flow">Flow information object.</param>
		Port AllocateFlow (FlowInformation flow);
		/// <summary>
		/// Deallocates the flow associated with the provided port.
		/// </summary>
		/// <param name="port">Port descriptor.</param>
		void DeallocateFlow (Port port);


		FlowState GetFlowState (Port port);

        /// <summary>
        /// Send synchronously sends data to the remote host using specified Port and returns the number of bytes successfully sent. 
        /// </summary>
        /// <param name="port">The Port that represents the destination location for the data. .</param>
        /// <param name="buffer">An array of type Byte that contains the data to be sent. </param>
        /// <param name="offset">The position in the data buffer at which to begin sending data. </param>
        /// <param name="size">The number of bytes to send. </param>
        /// <returns>The number of bytes sent.</returns>
        int Send(Port port, byte[] buffer, int offset, int size);

        /// <summary>
        /// Gets a value that specifies the size of the receive buffer of the Port.
        /// </summary>
        /// <returns>An Int32 that contains the size, in bytes, of the receive buffer. The default is 8192.</returns>
        int GetReceiveBufferSize(Port port);

        /// <summary>
        /// Sets a value that specifies the size of the receive buffer of the Port.
        /// </summary>
        /// <param name="port">A port object for which the size is to be set.</param>
        /// <param name="size">An Int32 that contains the size, in bytes, of the receive buffer. The default is 8192.</param>
        void SetReceiveBufferSize(Port port, int size);

        /// <summary>
        /// Gets a value that specifies the size of the send buffer of the Port.
        /// </summary>
        /// <returns>An Int32 that contains the size, in bytes, of the send buffer. The default is 8192.</returns>
        int GetSendBufferSize(Port port);

        /// <summary>
        /// Sets a value that specifies the size of the send buffer of the Port.
        /// </summary>
        /// <param name="port">A port object for which the size is to be set.</param>
        /// <param name="size">An Int32 that contains the size, in bytes, of the send buffer. The default is 8192.</param>
        void SetSendBufferSize(Port port, int size);

        /// <summary>
        /// Reads the available data from the specified port.
        /// </summary>
        /// <param name="port">Port object used for receiving data from.</param>
        /// <param name="buffer">An array of type Byte that is the storage location for received data. </param>
        /// <param name="offset">The position in the buffer parameter to store the received data. </param>
        /// <param name="size">The number of bytes to receive. </param>
        /// <returns>The number of bytes received.</returns>
        int Receive(Port port, byte[] buffer, int offset, int size);

		/// <summary>
		/// Registers the application name in the current IPC. An application serves new flows that are passed
		/// to the application using provided RequestHandler delegate.
		/// </summary>
		/// <param name="appInfo">App info.</param>
		/// <param name="reqHandler">Req handler.</param>
		void RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler);
		/// <summary>
		/// Deregisters the application.
		/// </summary>
		/// <param name="appInfo">App info.</param>
		void DeregisterApplication (ApplicationNamingInfo appInfo);
	}

}

