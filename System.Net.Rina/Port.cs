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
    public enum PortType
    {
        /// <summary>
        /// Stream oriented port type. 
        /// </summary>
        Stream = 1,
        /// <summary>
        /// Datagram (block) oriented port type.
        /// </summary>
        Dgram = 2,    
        /// <summary>
        /// Raw port type.
        /// </summary>
        Raw = 3,    
        /// <summary>
        /// Reliably delivered message oriented port type.
        /// </summary>
        Rdm = 4,
        /// <summary>
        /// Unknown port type.
        /// </summary>
        Unknown = -1,   

    }

    public enum PortShutdown
    {
        ///<summary>
        /// Shutdown port for receive.
        /// </summary>
        Receive = 0x00,
        ///<summary>
        /// Shutdown port for send.
        ///</summary>
        Send = 0x01,
        ///<summary>
        /// Shutdown socket for both send and receive.
        /// </summary>
        Both = 0x02,
    }

    public enum SelectMode
    {
        /// <devdoc>
        ///    <para>
        ///       Poll the read status of a port.
        ///    </para>
        /// </devdoc>
        SelectRead = 0,
        /// <devdoc>
        ///    <para>
        ///       Poll the write status of a port.
        ///    </para>
        /// </devdoc>
        SelectWrite = 1,
        /// <devdoc>
        ///    <para>
        ///       Poll the error status of a port.
        ///    </para>
        /// </devdoc>
        SelectError = 2
    } // 


    [Flags]
    public enum PortInformationOptions
    {
        NonBlocking = 0x1,
        Connected = 0x2,
    }
    /// <summary>
    /// Represents a port used to identify communication within DIF.
    /// </summary>
    public class Port
	{
        /// <summary>
        /// Defines default close timeout of all ports.
        /// </summary>
        internal const int DefaultCloseTimeout = -1;
        private int m_IntCleanedUp = 0;

        public IRinaIpc Ipc { get; private set; }
		public UInt64 Id { get; private set; }
        /// <summary>
        /// Gets or sets a value that indicates whether the Port is in blocking mode.
        /// </summary>
        public bool Blocking
        {
            get
            {
                return !this.Ipc.GetPortInformation(this).HasFlag(PortInformationOptions.NonBlocking);
            }
            set
            {
                Ipc.SetBlocking(this, value);    
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="System.Net.Rina.Port"/> is connected.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public bool Connected 
		{  
			get {
                return this.Ipc.GetPortInformation(this).HasFlag(PortInformationOptions.Connected);
			}		
		}

        internal bool CleanedUp
        {
            get
            {
                return (m_IntCleanedUp == 1);
            }
        }

        /// <summary>
        /// Gets the type of port. 
        /// </summary>
        public PortType PortType { get; internal set; }
        /// <summary>
        /// Gets the amoutn of available data that can be read from the port.
        /// </summary>
        public int Available
        {
            get
            {
                return Ipc.AvailableData(this);
            }        
        }

        internal Port (IRinaIpc ipc, UInt64 id)
		{
			this.Ipc = ipc;
			this.Id = id;
            this.PortType = PortType.Stream;
		}

        internal void InternalShutdown(PortShutdown direction)
        {
            throw new NotImplementedException();
        }

        internal void Close(int closeTimeout)
        {
            this.Ipc.DeallocateFlow(this);
        }

        /// <summary>
        /// Receives data from a connected port into a specific location of the receive buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        internal int Receive(byte[] buffer, int offset, int size)
        {
            return this.Ipc.Receive(this, buffer, offset, size);
        }

        internal int Send(byte[] buffer, int offset, int count)
        {
            return this.Ipc.Send(this, buffer, offset, count);
        }

        /// <summary>
        ///  Determines the status of the port.
        /// </summary>
        /// <param name="microSeconds">The time to wait for a response, in microseconds. </param>
        /// <param name="mode">One of the SelectMode values. </param>
        /// <returns>The status of the Port based on the polling mode value passed in the mode parameter.
        /// For SelectRead: true if data is available for reading or if the connection has been closed; otherwise, returns false. 
        /// For SelectWrite: true if data can be sent; otherwise, returns false. 
        /// For SelectError: true if connection has failed; otherwise, returns false.
        /// </returns>
        internal bool Poll(int microSeconds, SelectMode mode)
        {
            if (CleanedUp)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
            throw new NotImplementedException();
        }
    }
}

