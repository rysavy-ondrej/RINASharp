using System.Collections.Generic;
using System.Threading;

namespace System.Net.Rina
{
    public class PortAsyncEventArgs : EventArgs, IDisposable
    {
        internal Port curPort;
        IList<ArraySegment<byte>> _bufferList;
        bool disposed;
        int in_progress;
        EndPoint remote_ep;

        public PortAsyncEventArgs()
        {
            AcceptPort = null;
            Buffer = null;
            BufferList = null;
            BytesTransferred = 0;
            Count = 0;
            DisconnectReusePort = false;
            LastOperation = PortAsyncOperation.None;
            Offset = 0;
            RemoteEndPoint = null;
            SendPacketsSendSize = -1;
            PortError = PortError.Success;
            PortFlags = PortFlags.None;
            UserToken = null;
        }

        ~PortAsyncEventArgs()
        {
            Dispose(false);
        }

        public event EventHandler<PortAsyncEventArgs> Completed;

        public Port AcceptPort { get; set; }
        public byte[] Buffer { get; private set; }
        public IList<ArraySegment<byte>> BufferList
        {
            get { return _bufferList; }
            set
            {
                if (Buffer != null && value != null)
                    throw new ArgumentException("Buffer and BufferList properties cannot both be non-null.");
                _bufferList = value;
            }
        }

        public int BytesTransferred { get; internal set; }
        public Exception ConnectByNameError { get; internal set; }
        public int Count { get; internal set; }
        public bool DisconnectReusePort { get; set; }
        public PortAsyncOperation LastOperation { get; private set; }
        public int Offset { get; private set; }
        public PortError PortError { get; set; }

        public PortFlags PortFlags { get; set; }

        public EndPoint RemoteEndPoint
        {
            get { return remote_ep; }
            set { remote_ep = value; }
        }
        public int SendPacketsSendSize { get; set; }
        public object UserToken { get; set; }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void SetBuffer(int offset, int count)
        {
            SetBufferInternal(Buffer, offset, count);
        }

        public void SetBuffer(byte[] buffer, int offset, int count)
        {
            SetBufferInternal(buffer, offset, count);
        }

        internal void SetLastOperation(PortAsyncOperation op)
        {
            if (disposed)
                throw new ObjectDisposedException("System.Net.Ports.PortAsyncEventArgs");
            if (Interlocked.Exchange(ref in_progress, 1) != 0)
                throw new InvalidOperationException("Operation already in progress");
            LastOperation = op;
        }

        protected virtual void OnCompleted(PortAsyncEventArgs e)
        {
            if (e == null)
                return;

            e.Completed?.Invoke(e.curPort, e);
        }

        void Dispose(bool disposing)
        {
            disposed = true;

            if (disposing)
            {
                if (disposed || Interlocked.CompareExchange(ref in_progress, 0, 0) != 0)
                    return;
            }
            AcceptPort = null;
            Buffer = null;
            BufferList = null;
            RemoteEndPoint = null;
            UserToken = null;

        }
        void SetBufferInternal(byte[] buffer, int offset, int count)
        {
            if (buffer != null)
            {
                if (BufferList != null)
                    throw new ArgumentException("Buffer and BufferList properties cannot both be non-null.");

                int buflen = buffer.Length;
                if (offset < 0 || (offset != 0 && offset >= buflen))
                    throw new ArgumentOutOfRangeException("offset");

                if (count < 0 || count > buflen - offset)
                    throw new ArgumentOutOfRangeException("count");

                Count = count;
                Offset = offset;
            }
            Buffer = buffer;
        }

        #region Internals
        internal static AsyncCallback Dispatcher = new AsyncCallback(DispatcherCB);

        public enum PortAsyncOperation
        {
            Receive,
            Send,
            Disconnect,
            Connect,
            Accept,
            None
        }

        internal void DisconnectCallback(IAsyncResult ares)
        {
            try
            {
                curPort.EndDisconnect(ares);
            }
            catch (PortException ex)
            {
                PortError = ex.PortErrorCode;
            }
            catch (ObjectDisposedException)
            {
                PortError = PortError.OperationAborted;
            }
            finally
            {
                OnCompleted(this);
            }
        }

        internal void ReceiveCallback(IAsyncResult ares)
        {
            try
            {
                BytesTransferred = curPort.EndReceive(ares);
            }
            catch (PortException se)
            {
                PortError = se.PortErrorCode;
            }
            catch (ObjectDisposedException)
            {
                PortError = PortError.OperationAborted;
            }
            finally
            {
                OnCompleted(this);
            }
        }

        internal void SendCallback(IAsyncResult ares)
        {
            try
            {
                BytesTransferred = curPort.EndSend(ares);
            }
            catch (PortException se)
            {
                PortError = se.PortErrorCode;
            }
            catch (ObjectDisposedException)
            {
                PortError = PortError.OperationAborted;
            }
            finally
            {
                OnCompleted(this);
            }
        }

        static void DispatcherCB(IAsyncResult ares)
        {
            PortAsyncEventArgs args = (PortAsyncEventArgs)ares.AsyncState;
            if (Interlocked.Exchange(ref args.in_progress, 0) != 1)
                throw new InvalidOperationException("No operation in progress");
            PortAsyncOperation op = args.LastOperation;
            // Notes;
            // 	-PortOperation.AcceptReceive not used in PortAsyncEventArgs
            //	-SendPackets and ReceiveMessageFrom are not implemented yet
            if (op == PortAsyncOperation.Receive)
                args.ReceiveCallback(ares);
            else if (op == PortAsyncOperation.Send)
                args.SendCallback(ares);
            else if (op == PortAsyncOperation.Disconnect)
                args.DisconnectCallback(ares);
            else if (op == PortAsyncOperation.Connect)
                args.ConnectCallback();
            else
                throw new NotImplementedException(String.Format("Operation {0} is not implemented", op));

        }
        void ConnectCallback()
        {
            try
            {
                // PortError = (PortError)Worker.result.error;
                throw new NotImplementedException();
            }
            finally
            {
                OnCompleted(this);
            }
        }
        #endregion
    }
}