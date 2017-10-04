using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Rina.Shims.NamedPipes;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina
{
    public static class SendQueueFactory
    {
        public static SendQueueBlock CreateSimpleSendBuffer(ConnectionEndpoint cep)
        {
            var input = new BufferBlock<ArraySegment<byte>>();
            var emitter = new TransformBlock<ArraySegment<byte>, PipeMessage>(data =>
            {
                return new PipeDataMessage()
                {
                    TimestampCreated = DateTime.Now,
                    SourceAddress = cep.Information.SourceAddress,
                    DestinationAddress = cep.Information.DestinationAddress,
                    DestinationCepId = cep.RemoteCepId,
                    Data = data
                };
            });
            return new SendQueueBlock(input, emitter);
        }
    }

    public class CepIdSpace
    {
        private Helpers.UniqueRandomUInt64 m_space = new Helpers.UniqueRandomUInt64();

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
        public CepIdType(ulong value, bool isNullAllowed = false) : base(value, isNullAllowed)
        {
        }
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
        public readonly SendQueueBlock SendQueue;

        /// <summary>
        /// Provides information about the connection associated with the current ConnectionEndpoint.
        /// </summary>
        public ConnectionInformation Information;

        /// <summary>
        /// Keeps the record of incoming control messages waiting for processing by internal
        /// logic of the IPC connection manager.
        /// </summary>
        internal readonly BufferBlock<PipeMessage> ControlMessageBuffer = new BufferBlock<PipeMessage>();

        /// <summary>
        /// Stores <see cref="AddressFamily"/> of the current connection end point.
        /// </summary>
        private AddressFamily m_addressFamily;

        /// <summary>
        /// Stores <see cref="ConnectionType"/> of the current connection end point.
        /// </summary>
        private ConnectionType m_connectionType;

        private IpcProcessBase m_ipc;

        /// <summary>
        /// Stores the local connection end point identifier.
        /// </summary>
        private CepIdType m_localCepId;

        /// <summary>
        /// Stores the <see cref="Port"/> instance of the associated port or null if no port is bound to this connection end point.
        /// </summary>
        private Port m_port;

        /// <summary>
        /// Stores the remote connection end point identifier for connected connection or null for detached connection.
        /// </summary>
        private CepIdType m_remoteCepId;

        /// <summary>
        /// Stores the state of the current <see cref="ConnectionEndpoint"/>.
        /// </summary>
        private ConnectionState m_state = ConnectionState.Detached;

        public ConnectionEndpoint(AddressFamily af, ConnectionType ct, CepIdType cepId) : this()
        {
            this.m_addressFamily = af;
            this.m_connectionType = ct;
            this.m_localCepId = cepId;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ConnectionEndpoint"/> initialized to default value.
        /// </summary>
        protected ConnectionEndpoint()
        {
            ReceiveBufferSize = 4096 * 4;
            ReceiveTimeout = TimeSpan.FromSeconds(30);
            Blocking = true;
            SendQueue = SendQueueFactory.CreateSimpleSendBuffer(this);
        }

        /// <summary>
        /// Represents an address family used with this connection end point.
        /// </summary>
        public AddressFamily AddressFamily { get; internal set; }

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
        /// Local ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public CepIdType LocalCepId { get { return m_localCepId; } }

        /// <summary>
        /// A port associated with the connection end point. This can be null if
        /// the connection is not bound.
        /// </summary>
        public Port Port { get { return m_port; } }

        /// <summary>
        /// Gets the size, in bytes, of the inbound buffer for a connection.
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

        /// <summary>
        /// Remote ConnectionEndPoint Id is used to identify the connection.
        /// </summary>
        public CepIdType RemoteCepId { get { return m_remoteCepId; } }

        /// <summary>
        /// Gets the size, in bytes, of the outbound buffer for a connection.
        /// </summary>
        public int SendBufferSize { set; get; }

        /// <summary>
        /// Gets a <see cref="Task"/> that represents completion of the send block.
        /// </summary>
        public abstract Task SendCompletion
        {
            get;
        }

        /// <summary>
        /// Gets the current state of the <see cref="ConnectionEndpoint"/>. State can be changed
        /// using <see cref="Open(CepIdType)"/>, <see cref="Close(bool)"/>, <see cref="ReadOnly"/>, or
        /// <see cref="WriteOnly"/> method.
        /// </summary>
        public virtual ConnectionState State
        {
            get
            {
                return (m_ipc == null) ? ConnectionState.Detached : m_ipc.GetConnectionState(this);
            }
        }

        /// <summary>
        /// Asynchronously sends message without buffering. Useful for operations may require to
        /// bypass the usual processing data path.
        /// </summary>
        /// <param name="message">A message to be sent.</param>
        public abstract Task SendMessageAsync(PipeMessage message);

        /// <summary>
        /// Returns a <see cref="String"/> describing the current object.
        /// </summary>
        /// <returns>A <see cref="String"/> describing the current object.</returns>
        public override string ToString()
        {
            return $"{Information.SourceApplication}@{Information.SourceAddress}:{LocalCepId} --> {Information.DestinationApplication}@{Information.DestinationAddress}:{RemoteCepId} [{Connected}]";
        }

        /// <summary>
        /// Binds the port to the current connection.
        /// </summary>
        /// <param name="port"></param>
        internal void BindPort(Port port)
        {
            m_port = port;
        }

        /// <summary>
        /// Closes the ConnectionEndpoint.
        /// </summary>
        /// <param name="abort"></param>
        internal void Close(bool abort = false)
        {
            if (abort) OnAbort();
            else OnClose();
        }

        /// <summary>
        /// Opens the connection by associating the connection with the remote CepId.
        /// </summary>
        /// <param name="remoteCepId"></param>
        internal void Open(CepIdType remoteCepId)
        {
            m_remoteCepId = remoteCepId;
        }

        /// <summary>
        /// Called when connection is aborted. It removes all items from <see cref="SendQueue"/>  and
        /// call <see cref="IDataflowBlock.Complete"/> method of
        /// <see cref="SendQueue"/> object.
        /// </summary>
        protected virtual void OnAbort()
        {
            IList<ArraySegment<byte>> remainingItems = null;
            if (SendQueue.DiscardAll(out remainingItems))
            {
                Trace.TraceInformation($"{nameof(OnAbort)}: There were {remainingItems.Count} messages in the {nameof(SendQueue)}.");
            }
            SendQueue.Complete();
        }

        /// <summary>
        /// Called when connection is closed. It just call <see cref="IDataflowBlock.Complete"/> method of
        /// <see cref="SendQueue"/> object. Call this method from derived classes if you do not implement a
        /// specific completion code for <see cref="SendQueue"/>.
        /// </summary>
        protected virtual void OnClose()
        {
            SendQueue.Complete();
        }

        /// <summary>
        /// Called when state is to be changed from the <paramref name="oldValue"/> to <paramref name="newValue"/>.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void OnStateChanged(ConnectionState oldValue, ConnectionState newValue) { }
    }

    public class SendQueueBlock : IPropagatorBlock<ArraySegment<byte>, PipeMessage>
    {
        private BufferBlock<ArraySegment<byte>> m_inputBuffer;
        private TransformBlock<ArraySegment<byte>, PipeMessage> m_emitter;
        private IPropagatorBlock<ArraySegment<byte>, PipeMessage> m_block;
        
        public SendQueueBlock(BufferBlock<ArraySegment<byte>> inputBuffer, TransformBlock<ArraySegment<byte>, PipeMessage> emitter) 
        {
            m_inputBuffer = inputBuffer;
            m_emitter = emitter;
            m_inputBuffer.LinkTo(m_emitter, new DataflowLinkOptions() { PropagateCompletion = true });
            m_block = DataflowBlock.Encapsulate(m_inputBuffer, m_emitter);            
        }

        public Task Completion
        {
            get
            {
                return m_block.Completion;
            }
        }

        public void Complete()
        {
            m_block.Complete();
        }

        public PipeMessage ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<PipeMessage> target, out bool messageConsumed)
        {
            return m_block.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            m_block.Fault(exception);
        }

        public IDisposable LinkTo(ITargetBlock<PipeMessage> target, DataflowLinkOptions linkOptions)
        {
            return m_block.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, ArraySegment<byte> messageValue, ISourceBlock<ArraySegment<byte>> source, bool consumeToAccept)
        {
            return m_block.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<PipeMessage> target)
        {
            m_block.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<PipeMessage> target)
        {
            return m_block.ReserveMessage(messageHeader, target);
        }

        internal bool DiscardAll(out IList<ArraySegment<byte>> remainingItems)
        {
            return m_inputBuffer.TryReceiveAll(out remainingItems);
        }
    }
}