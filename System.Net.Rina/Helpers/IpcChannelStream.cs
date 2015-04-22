//
//  IpcChannelStream.cs
//
//  This class is an adaptation of System.Net.Sockets.NetworkStream class for
//  RINA port object.
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2015 PRISTINE
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
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    public class IpcChannelStream : Stream
    {
        /// <devdoc>
        ///    <para>
        ///       Used by the class to hold the underlying socket the stream uses.
        ///    </para>
        /// </devdoc>
        private Port m_StreamPort;

        /// <devdoc>
        ///    <para>
        ///       Used by the class to indicate that the stream is m_Readable.
        ///    </para>
        /// </devdoc>
        private bool m_Readable;

        /// <devdoc>
        ///    <para>
        ///       Used by the class to indicate that the stream is writable.
        ///    </para>
        /// </devdoc>
        private bool m_Writeable;

        private bool m_OwnsPort;

        public IpcChannelStream(Port port)
        {

            if (port == null)
            {
                throw new ArgumentNullException("socket");
            }
            InitStream(port, FileAccess.ReadWrite);
        }

        public IpcChannelStream(Port port, bool ownsPort)
        {
                if (port == null)
                {
                    throw new ArgumentNullException("port");
                }
                InitStream(port, FileAccess.ReadWrite);
                m_OwnsPort = ownsPort;
        }

        public IpcChannelStream(Port port, FileAccess access)
        {

                if (port == null)
                {
                    throw new ArgumentNullException("port");
                }
                InitStream(port, access);

        }
        public IpcChannelStream(Port port, FileAccess access, bool ownsPort)
        { 
                if (port == null)
                {
                    throw new ArgumentNullException("port");
                }
                InitStream(port, access);
                m_OwnsPort = ownsPort;
        }


        protected Port Port
        {
            get
            {
                return m_StreamPort;
            }
        }

        internal void InitStream(Port port, FileAccess Access)
        {
            //
            // parameter validation
            //
            if (!port.Blocking)
            {
                throw new IOException("Port is not set in blocking mode.");
            }
            if (!port.Connected)
            {
                throw new IOException("Port is not connected.");
            }
            if (port.PortType != PortType.Stream)
            {
                throw new IOException("PortType is not Stream.");
            }

            m_StreamPort = port;

            switch (Access)
            {
                case FileAccess.Read:
                    m_Readable = true;
                    break;
                case FileAccess.Write:
                    m_Writeable = true;
                    break;
                case FileAccess.ReadWrite:
                default: // assume FileAccess.ReadWrite
                    m_Readable = true;
                    m_Writeable = true;
                    break;
            }

        }

        internal Port InternalPort
        {
            get
            {
                Port chkSocket = m_StreamPort;
                if (m_CleanedUp || chkSocket == null)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return chkSocket;
            }
        }

        internal void InternalAbortSocket()
        {
            if (!m_OwnsSocket)
            {
                throw new InvalidOperationException();
            }

            Port chkPort = m_StreamPort;
            if (m_CleanedUp || chkPort == null)
            {
                return;
            }

            try
            {
                chkPort.Close(0);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal void ConvertToNotSocketOwner()
        {
            m_OwnsSocket = false;
            // Suppress for finialization still allow proceed the requests
            GC.SuppressFinalize(this);
        }




        public override bool CanRead
        {
            get
            {
                return m_Readable;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return m_Writeable;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return true; // should we check for Connected state?
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public virtual bool DataAvailable
        {
            get
            {
                if (m_CleanedUp)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                Port chkStreamSocket = m_StreamPort;
                if (chkStreamSocket == null)
                {
                    throw new IOException("Port is closed.");
                }

                // Ask the socket how many bytes are available. If it's
                // not zero, return true.

                return chkStreamSocket.Available != 0;
            }
        }




        /// <summary>
        /// Flushes data from the stream.  This is meaningless for us, so it does nothing.
        /// </summary>
        public override void Flush()
        {
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.Delay(1);
        }


        internal bool PollRead()
        {
            if (m_CleanedUp)
            {
                return false;
            }
            Port chkStreamPort = m_StreamPort;
            if (chkStreamPort == null)
            {
                return false;
            }
            return chkStreamPort.Poll(0, SelectMode.SelectRead);
        }

        internal bool Poll(int microSeconds, SelectMode mode)
        {
            if (m_CleanedUp)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            Port chkStreamSocket = m_StreamPort;
            if (chkStreamSocket == null)
            {
                throw new IOException("Cannot poll port. Connection closed.");
            }

            return chkStreamSocket.Poll(microSeconds, mode);
        }

        public override int Read([In, Out] byte[] buffer, int offset, int size)
        {
                bool canRead = CanRead;  // Prevent race with Dispose.
                if (m_CleanedUp)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                if (!canRead)
                {
                    throw new InvalidOperationException("Cannot read write-only stream.");
                }
                //
                // parameter validation
                //
                if (buffer == null)
                {
                    throw new ArgumentNullException("buffer");
                }
                if (offset < 0 || offset > buffer.Length)
                {
                    throw new ArgumentOutOfRangeException("offset");
                }
                if (size < 0 || size > buffer.Length - offset)
                {
                    throw new ArgumentOutOfRangeException("size");
                }


                Port chkStreamPort = m_StreamPort;
                if (chkStreamPort == null)
                {
                    throw new IOException("Cannot read from the port. Connection was closed. ");
                }

                try
                {
                    int bytesTransferred = chkStreamPort.Receive(buffer, offset, size);
                    return bytesTransferred;
                }
                catch (Exception exception)
                {
                    if (exception is ThreadAbortException || exception is StackOverflowException || exception is OutOfMemoryException)
                    {
                        throw;
                    }

                    //
                    // some sort of error occured on the socket call,
                    // set the SocketException as InnerException and throw
                    //
                    throw new IOException("Read port error", exception);
                }

        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes data to the stream. Since the underlaying port is in blocking mode all bytes will be written or
        /// exceoption is thrown, e.g., in case of closing port before completing operation.
        /// </summary>
        /// <param name="buffer">Buffer to write from.</param>
        /// <param name="offset">Offset into the buffer from where we'll start writing.</param>
        /// <param name="count">Number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            bool canWrite = CanWrite; // Prevent race with Dispose.
            if (m_CleanedUp)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
            if (!canWrite)
            {
                throw new InvalidOperationException("Cannot write to readonly stream.");
            }
            //
            // parameter validation
            //
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if (count < 0 || count > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException("size");
            }


            Port chkStreamPort = m_StreamPort;
            if (chkStreamPort == null)
            {
                throw new IOException("Cannot write to port. Connection closed.");
            }

            try
            {
                //
                // since the socket is in blocking mode this will always complete
                // after ALL the requested number of bytes was transferred
                //
                chkStreamPort.Send(buffer, offset, count);
            }
            catch (Exception exception)
            {
                if (exception is ThreadAbortException || exception is StackOverflowException || exception is OutOfMemoryException)
                {
                    throw;
                }

                //
                // some sort of error occured on the socket call,
                // set the SocketException as InnerException and throw
                //
                throw new IOException("Cannot write to port", exception);
            }
        }


        public void Close(int timeout)
        {
                if (timeout < -1)
                {
                    throw new ArgumentOutOfRangeException("timeout");
                }
                m_CloseTimeout = timeout;
                Close();
        }

        private volatile bool m_CleanedUp = false;
        private bool m_OwnsSocket;
        private int m_CloseTimeout = Port.DefaultCloseTimeout;

        protected override void Dispose(bool disposing)
        {
            bool cleanedUp = m_CleanedUp;
            m_CleanedUp = true;
            if (!cleanedUp && disposing)
            {
                if (m_StreamPort != null)
                {
                    m_Readable = false;
                    m_Writeable = false;
                    if (m_OwnsSocket)
                    {
                        //
                        // if we own the Socket (false by default), close it
                        // ignoring possible exceptions (eg: the user told us
                        // that we own the Socket but it closed at some point of time,
                        // here we would get an ObjectDisposedException)
                        //
                        Port chkStreamSocket = m_StreamPort;
                        if (chkStreamSocket != null)
                        {
                            chkStreamSocket.InternalShutdown(PortShutdown.Both);
                            chkStreamSocket.Close(m_CloseTimeout);
                        }
                    }
                }
                base.Dispose(disposing);
            }
        }

        ~IpcChannelStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// Indicates whether the stream is still connected.
        /// </summary>
        internal bool Connected
        {
            get
            {
                Port port = m_StreamPort;
                if (!m_CleanedUp && port != null && port.Connected)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
