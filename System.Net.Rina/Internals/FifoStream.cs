using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace System.IO
{
    public class FifoStream : Stream
    {
                                                                        
        private object _lockForRead;
        private object _lockForAll;
        private Queue<byte[]> _chunks;
        private byte[] _currentChunk;
        private int _currentChunkPosition;
        private ManualResetEvent _doneWriting;
        private ManualResetEvent _dataAvailable;
        private WaitHandle[] _events;
        private int _doneWritingHandleIndex;
        private volatile bool _illegalToWrite;
        private int _capacity;
        private int _freespace;
        public FifoStream(int capacity)
        {
            _capacity = capacity;
            _freespace = capacity;
            _chunks = new Queue<byte[]>();
            _doneWriting = new ManualResetEvent(false);
            _dataAvailable = new ManualResetEvent(false);
            _events = new WaitHandle[] { _dataAvailable, _doneWriting };
            _doneWritingHandleIndex = 1;
            _lockForRead = new object();
            _lockForAll = new object();
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return !_illegalToWrite; } }

        public override void Flush() { }
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
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
        /// Gets amount of data available in the stream for reading.
        /// </summary>
        /// <returns></returns>
        public int AvailableBytes
        {
            get { return this._capacity - this._freespace; }
        }

        /// <summary>
        /// Gets the amount of space in the stream that can be used for storing new data.
        /// </summary>
        public int FreeSpace
        {
            get { return this._freespace; }
        }

        /// <summary>
        /// This method implements waiting for available data.
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout specified in ms. Use <c>Timeout.Infinite</c> for infinite waiting.</param>
        public void WaitForData(int millisecondsTimeout)
        {            
            _dataAvailable.WaitOne(millisecondsTimeout);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("count");
            if (_dataAvailable == null)
                throw new ObjectDisposedException(GetType().Name);

            if (count == 0) return 0;

            while (true)
            {
                int handleIndex = WaitHandle.WaitAny(_events);
                lock (_lockForRead)
                {
                    lock (_lockForAll)
                    {
                        if (_currentChunk == null)
                        {
                            if (_chunks.Count == 0)
                            {
                                if (handleIndex == _doneWritingHandleIndex)
                                    return 0;
                                else continue;
                            }
                            _currentChunk = _chunks.Dequeue();
                            _currentChunkPosition = 0;
                        }
                    }

                    int bytesAvailable =
                        _currentChunk.Length - _currentChunkPosition;
                    int bytesToCopy;
                    if (bytesAvailable > count)
                    {
                        bytesToCopy = count;
                        Buffer.BlockCopy(_currentChunk, _currentChunkPosition,
                            buffer, offset, count);
                        _currentChunkPosition += count;
                    }
                    else
                    {
                        bytesToCopy = bytesAvailable;
                        Buffer.BlockCopy(_currentChunk, _currentChunkPosition,
                            buffer, offset, bytesToCopy);
                        _currentChunk = null;
                        _currentChunkPosition = 0;
                        lock (_lockForAll)
                        {
                            if (_chunks.Count == 0) _dataAvailable.Reset();
                        }
                    }
                    this._freespace += bytesToCopy;
                    return bytesToCopy;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("count");
            if (_dataAvailable == null)
                throw new ObjectDisposedException(GetType().Name);
            if (count > this._freespace)
                throw new ArgumentOutOfRangeException("count", "buffer has not enough capacity");

            if (count == 0) return;

            byte[] chunk = new byte[count];
            Buffer.BlockCopy(buffer, offset, chunk, 0, count);
            lock (_lockForAll)
            {
                if (_illegalToWrite)
                    throw new InvalidOperationException(
                        "Writing has already been completed.");
                _chunks.Enqueue(chunk);
                _dataAvailable.Set();
                _freespace -= count;
            }
        }

        public void SetEndOfStream()
        {
            if (_dataAvailable == null)
                throw new ObjectDisposedException(GetType().Name);
            lock (_lockForAll)
            {
                _illegalToWrite = true;
                _doneWriting.Set();
            }
        }

        public override void Close()
        {
            base.Close();
            if (_dataAvailable != null)
            {
                _dataAvailable.Close();
                _dataAvailable = null;
            }
            if (_doneWriting != null)
            {
                _doneWriting.Close();
                _doneWriting = null;
            }
        }
    }
}
