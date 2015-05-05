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
using System.Threading;
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
    public delegate ConnectionRequestResult ConnectionRequestHandler(IRinaIpc context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler);

    /// <summary>
    /// This delegate is used to inform application that accepted a new request about the created flow. In particulat, application 
    /// to serve the new request has to know port used for further communication.
    /// </summary>
    /// <param name="context">The IpcContext object which manages the communication.</param>
    /// <param name="flowInformation">The flow information describing communicaton parties.</param>
    /// <param name="port">The port object used to communication with other end point.</param>
    public delegate void AcceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port);
	/// <summary>
	/// This interface represents basic IPC API.
	/// </summary>
	public interface IRinaIpc {

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
        /// <summary>
        /// Sets whether the given port will have blocking or non-blocking behavior.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="value"></param>
        void SetBlocking(Port port, bool value);

        /// <summary>
        /// Gets information about the specified port.
        /// </summary>
        /// <param name="port">The port for which information is to be retrieved.</param>
        /// <returns>PortInformationOptions for the given port.</returns>
        PortInformationOptions GetPortInformation (Port port);

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
        /// Reads the available data from the specified port. Depending on port settings, data are read by 
        /// alinged to SDU boundaries. 
        /// </summary>
        /// <param name="port">Port object used for receiving data from.</param>
        /// <returns>Buffer containing data received.</returns>
        byte[] Receive(Port port);



        bool DataAvailable(Port port);

        /// <summary>
        /// Provides a Task that asynchronously monitors the source for available output. 
        /// </summary>
        /// <param name="port">Port for which the handle is created.</param>
        /// <returns>A Task that informs of whether and when more output is available. If, when the task completes, 
        /// its Result is true, more output is available in the source (though another consumer of the source may retrieve the data). 
        /// If it returns false, more output is not and will never be available, due to the source completing prior to output being available.
        /// </returns>
        Task<bool> DataAvailableAsync(Port port);

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

