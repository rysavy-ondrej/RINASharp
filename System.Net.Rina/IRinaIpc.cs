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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace System.Net.Rina
{

    public enum DeregisterApplicationOption { WaitForCompletition, DisconnectClients }
    public class ApplicationInstanceHandle
    {
        /// <summary>
        /// Value that identifies an instance of the application.
        /// </summary>
        public Guid Handle { internal get; set; }

        public ApplicationInstanceHandle()
        {
            Handle = Guid.NewGuid();
        }
    }
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
    /// This delegate is used to inform application that accepted a new request about the created flow. In particular, application 
    /// to serve the new request has to know port used for further communication.
    /// </summary>
    /// <param name="context">The IpcContext object which manages the communication.</param>
    /// <param name="flowInformation">The flow information describing communication parties.</param>
    /// <param name="port">The port object used to communication with other end point.</param>
    public delegate Task AcceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port);
	/// <summary>
	/// This interface represents basic IPC API.
	/// </summary>
	public interface IRinaIpc : IDisposable {

		Address LocalAddress { get; }



        /// <summary>
        /// Creates a new connection and connects it to a new <see cref="Port"/>.
        /// </summary>
        /// <returns>The flow.</returns>
        /// <param name="flow">Flow information object.</param>
        Port Connect (FlowInformation flow);
		/// <summary>
		/// Disconnects the specifies port from the connection and closes the connection.
		/// </summary>
		/// <param name="port">Port descriptor.</param>
		void Disconnect (Port port, TimeSpan timeout);

        /// <summary>
        /// Aborts the connection of the specified <see cref="Port"/>. 
        /// See documentation for details on the difference between <see cref="Abort(Port)"/> and <see cref="Disconnect(Port)"/>.
        /// </summary>
        /// <param name="port"></param>
        void Abort(Port port);


        void Close(Port port);

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
        /// Reads the available data from the specified port. 
        /// </summary>
        /// <param name="port">Port object used for receiving data from.</param>
        /// <returns>Buffer containing data received.</returns>
        int Receive(Port port, byte[] buffer, int offset, int size, PortFlags socketFlags);


        /// <summary>
        /// Tests if <see cref="Port"/> has some available data.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        bool DataAvailable(Port port);

        /// <summary>
        /// Provides a Task that asynchronously monitors the source for available output. 
        /// </summary>
        /// <param name="port">Port for which the handle is created.</param>
        /// <returns>A Task that informs of whether and when more output is available. If, when the task completes, 
        /// its Result is true, more output is available in the source (though another consumer of the source may retrieve the data). 
        /// If it returns false, more output is not and will never be available, due to the source completing prior to output being available.
        /// </returns>
        Task<bool> DataAvailableAsync(Port port, CancellationToken ct);

        /// <summary>
        /// Registers the application name in the current IPC. An application serves new flows that are passed
        /// to the application using provided RequestHandler delegate.
        /// </summary>
        /// <param name="appInfo">An application information describing the application.</param>
        /// <param name="reqHandler">Request handler used to determine if new connection can be accepted.</param>
        /// <returns><see cref="ApplicationInstanceHandle"/> that identifies the registered application. </returns>
        ApplicationInstanceHandle RegisterApplication (ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler);
		/// <summary>
		/// Removes the registration of the application specified by <see cref="ApplicationInstanceHandle"/>.
		/// </summary>
		/// <param name="appInfo"><see cref="ApplicationInstanceHandle"/> that identifies application instance to remove.</param>
		void DeregisterApplication (ApplicationInstanceHandle appInfo, DeregisterApplicationOption option, TimeSpan timeout);
	}

}

