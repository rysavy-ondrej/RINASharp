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
        /// Indicates that DisconnectResponse was received for this connection.
        /// </summary>
        internal ManualResetEventSlim DisconnectResponseReceived = new ManualResetEventSlim(false);
        internal ManualResetEventSlim SendCompletition = new ManualResetEventSlim(false);

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
        protected ConnectionState m_state = ConnectionState.Detached;
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
        /// Called when state is to be changed from the <paramref name="oldValue"/> to <paramref name="newValue"/>.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void OnStateChanged(ConnectionState oldValue, ConnectionState newValue)
        {  }

        public ConnectionEndpoint()
        {
            ReceiveBufferSize = 4096 * 4;
            ReceiveTimeout = TimeSpan.FromSeconds(30);
            Blocking = true;
        }

        /// <summary>
        /// Provides information about available space in the receive buffer.
        /// </summary>
        public abstract int ReceiveBufferSpace { get; }

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
        public bool Connected { get; internal set; }

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
        /// An amount of time for which the IPC process will wait until the completion of receive operation.
        /// </summary>
        public TimeSpan ReceiveTimeout { set; get; }


        /// <summary>
        /// Returns a <see cref="String"/> describing the current object.
        /// </summary>
        /// <returns>A <see cref="String"/> describing the current object.</returns>
        public override string ToString()
        {
            return $"{Information.SourceApplication}@{Information.SourceAddress}:{LocalCepId} --> {Information.DestinationApplication}@{Information.DestinationAddress}:{RemoteCepId} [{Connected}]";
        }
    }
}
