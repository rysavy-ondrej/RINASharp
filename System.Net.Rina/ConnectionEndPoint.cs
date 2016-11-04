using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina
{
    public class CepIdSpace
    {
        Helpers.UniqueRandomUInt64 m_space = new Helpers.UniqueRandomUInt64();

        public CepIdType Next()
        {
            return new CepIdType(m_space.Next());
        }
        public void Release(CepIdType val)
        {
            m_space.Release(val);
        }
    }

    public class CepIdType : Kleinware.LikeType.LikeType<UInt64>
    {
        public CepIdType(ulong value, bool isNullAllowed = false) : base(value, isNullAllowed) {  }
    }

    /// <summary>
    /// This class is used to maintain a local endpoint of the connection.
    /// </summary>
    public abstract class ConnectionEndpoint
    {
        /// <summary>
        /// This is a receive queue. New data are enqueued in this queue by the IPC process.
        /// </summary>
        public readonly ReceiveBufferBlock<byte> ReceiveQueue = new ReceiveBufferBlock<byte>();

        /// <summary>
        /// This is a send queue that stores data before it is sent by the IPC process. Because this
        /// implements <see cref="ISourceBlock{TOutput}"/> it is possible to link this queue to the
        /// sender block which enables the implementor to create an arbitrary a processing pipeline.
        /// </summary>
        public readonly BufferBlock<ArraySegment<byte>> SendQueue = new BufferBlock<ArraySegment<byte>>();
        
        /// <summary>
        /// Provides information about the connection associated with the current ConnectionEndpoint.
        /// </summary>
        public ConnectionInformation Information;

        /// <summary>
        /// Local ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public CepIdType LocalCepId;

        /// <summary>
        /// Remote ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public CepIdType RemoteCepId;

        /// <summary>
        /// A port associated with the connection end point. This can be null if 
        /// the connection is not bound. 
        /// </summary>
        internal Port Port;

        /// <summary>
        /// Indicates that all data were successfully send from the send queue and the queue does not
        /// accept any more data. This needs to be set in the derived class as completion can be
        /// confirmed by the last <see cref="ActionBlock{TInput}"/> of the sender pipeline.
        /// </summary>
        internal ManualResetEventSlim UpflowClosedEvent = new ManualResetEventSlim(false);

        /// <summary>
        /// Stores the state of the current <see cref="ConnectionEndpoint"/>.
        /// </summary>
        protected ConnectionState m_state = ConnectionState.Detached;
        /// <summary>
        /// Creates a new instance of <see cref="ConnectionEndpoint"/> initialized to default value.
        /// </summary>
        public ConnectionEndpoint()
        {
            ReceiveBufferSize = 4096 * 4;
            ReceiveTimeout = TimeSpan.FromSeconds(30);
            Blocking = true;
        }

        /// <summary>
        /// Represents an address family used with this connection end point.
        /// </summary>
        public Sockets.AddressFamily AddressFamily { get; internal set; }

        /// <summary>
        /// Specifies whether the port is blocking or non-blocking.
        /// </summary>
        public bool Blocking { get; internal set; }

        /// <summary>
        /// Determines if the current endpoint is connected or disconnected.
        /// </summary>
        public bool Connected
        {
            get
            { return m_state == ConnectionState.Open; }
        }

        /// <summary>
        /// Represents connection type of this connection end point.
        /// </summary>
        public ConnectionType ConnectionType { get; internal set; }

        public CepIdType Id { get { return LocalCepId; } }

        /// <summary>
        /// Sets or gets the maximum number of bytes that can be buffered.
        /// </summary>
        public int ReceiveBufferSize { set; get; }

        /// <summary>
        /// Provides information about available space in the receive buffer.
        /// </summary>
        public int ReceiveBufferSpace
        {
            get
            {
                return ReceiveBufferSize - ReceiveQueue.ItemsAvailable;
            }
        }

        /// <summary>
        /// An amount of time for which the IPC process will wait until the completion of receive operation.
        /// </summary>
        public TimeSpan ReceiveTimeout { set; get; }

        public virtual ConnectionState State
        {
            get
            {
                return m_state;
            }
            internal set
            {
                if (m_state != value)
                {
                    OnStateChanged(m_state, value);
                    m_state = value;
                }
            }
        }
        /// <summary>
        /// Returns a <see cref="String"/> describing the current object.
        /// </summary>
        /// <returns>A <see cref="String"/> describing the current object.</returns>
        public override string ToString()
        {
            return $"{Information.SourceApplication}@{Information.SourceAddress}:{LocalCepId} --> {Information.DestinationApplication}@{Information.DestinationAddress}:{RemoteCepId} [{Connected}]";
        }

        /// <summary>
        /// Called when state is to be changed from the <paramref name="oldValue"/> to <paramref name="newValue"/>.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void OnStateChanged(ConnectionState oldValue, ConnectionState newValue)
        {
            if (oldValue == ConnectionState.Open && newValue == ConnectionState.Closed)
                OnAbort();
            if (oldValue == ConnectionState.Open && newValue == ConnectionState.Closing)
                OnClose();
        }

        /// <summary>
        /// Called when connection is aborted. It removes all items from <see cref="SendQueue"/>  and
        /// call <see cref="IDataflowBlock.Complete"/> method of
        /// <see cref="SendQueue"/> object.
        /// </summary>
        protected virtual void OnAbort()
        {
            IList<ArraySegment<byte>> remainingItems = null;
            if (SendQueue.TryReceiveAll(out remainingItems))
            {
                Trace.TraceInformation($"{nameof(OnAbort)}: There were {remainingItems.Count} messages in the {nameof(SendQueue)}.");
            }
            SendQueue.Complete();
        }
        /// <summary>
        /// Called when connection is closed. It just call <see cref="IDataflowBlock.Complete"/> method of
        /// <see cref="SendQueue"/> object.
        /// </summary>
        protected virtual void OnClose()
        {
            SendQueue.Complete();
        }
    }
}
