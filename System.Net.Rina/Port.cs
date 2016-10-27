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

namespace System.Net.Rina
{

    public class PortIdType : Kleinware.LikeType.LikeType<UInt32>
    {
        public PortIdType(uint value, bool isNullAllowed = false) : base(value, isNullAllowed)
        {
        }
    }

    public class PortIdSpace
    {
        Helpers.UniqueRandomUInt32 m_space = new Helpers.UniqueRandomUInt32();

        public PortIdType Next()
        {
            return new PortIdType(m_space.Next());
        }
        public void Release(PortIdType val)
        {
            m_space.Release(val);
        }
    }

    [Flags]
    public enum PortFlags
    {
        None = 0x00000000,
        OutOfBand = 0x00000001,
        Peek = 0x00000002,
        DontRoute = 0x00000004,
        MaxIOVectorLength = 0x00000010,
        Truncated = 0x00000100,
        ControlDataTruncated = 0x00000200,
        Broadcast = 0x00000400,
        Multicast = 0x00000800,
        Partial = 0x00008000,
    }

    [Flags]
    public enum PortInformationOptions : int
    {
        NonBlocking = 0x1,
        Connected = 0x2,
    }
 

    public enum PortState
    {
        Connecting, 
        Open, 
        ReadOnly,
        WriteOnly,
        Closed
    }

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
    /// <summary>
    /// Represents a port used to identify communication within DIF.
    /// </summary>
    public class Port : IDisposable
    {
        /// <summary>
        /// Defines default close timeout of all ports.
        /// </summary>
        internal const int DefaultCloseTimeout = -1;

    
        private int m_IntCleanedUp = 0;

        internal Port(IRinaIpc ipc, PortIdType id)
        {
            Ipc = ipc;
            Id = id;
            PortType = PortType.Stream;
        }

        public Sockets.AddressFamily AddressFamily { get; internal set; }

        /// <summary>
        /// Gets true id data are available for reading through this port; otherwise it returns false.
        /// </summary>
        public bool Available
        {
            get
            {
                return Ipc.DataAvailable(this);
            }
        }

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
            get
            {
                return this.Ipc.GetPortInformation(this).HasFlag(PortInformationOptions.Connected);
            }
        }

        /// <summary>
        /// <see cref="ulong"/> value representing a unique identifier of the <see cref="Port"/> within the current IPC.  
        /// </summary>
		public PortIdType Id { get; private set; }

        /// <summary>
        /// IPC object represented by its <see cref="IRinaIpc"/> interface that owns this <see cref="Port"/>. 
        /// </summary>
        public IRinaIpc Ipc { get; private set; }

        // public ConnectionEndPoint LocalEndPoint { get; }
        public bool NoDelay { get; set; }

        /// <summary>
        /// Gets the type of the current <see cref="Port"/> object. 
        /// </summary>
        public PortType PortType { get; internal set; }

        // public ProtocolType ProtocolType { get; }
        public int ReceiveBufferSize { get; set; }

        // public EndPoint RemoteEndPoint { get; }
        public int SendBufferSize { get; set; }

        public short Ttl { get; set; }

        /// <summary>
        /// Points to associated connection end point. If the port is disconnected then this value is 0.
        /// </summary>
        internal CepIdType CepId { get; set; }

        internal bool CleanedUp
        {
            get
            {
                return (m_IntCleanedUp == 1);
            }
        }

        public void Dispose() { }


        public void Shutdown(TimeSpan timeout)
        {
            Ipc.Disconnect(this, timeout);
            m_IntCleanedUp = 1;
        }

        internal void Close(int closeTimeout)
        {
            Ipc.Abort(this);
            m_IntCleanedUp = 1;
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
        /// <summary>
        /// Receives data from a connected port into a specific location of the receive buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public int Receive(byte[] buffer,int offset,int size, PortFlags portFlags)
        {
            return this.Ipc.Receive(this, buffer, offset, size, portFlags);
        }

        public int Send(byte[] buffer, int offset, int count)
        {
            return this.Ipc.Send(this, buffer, offset, count);
        }


        /// <summary>
        /// Reads data from the given <see cref="Port"/>.
        /// </summary>
        /// <param name="port"></param>
        /// <returns>A byte array containing received data. If operation would block than the result is zero length array. </returns>
        public byte[] Receive()
        {
            var buffer = new byte[4096];
            var bytesRead = Receive(buffer, 0, buffer.Length, PortFlags.None);
            if (bytesRead > 0)
            {
                var result = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, result, 0, bytesRead);
                return result;
            }
            else return null;                
        }
    }
}

