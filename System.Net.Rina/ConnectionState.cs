namespace System.Net.Rina
{
    /// <summary>
    /// Specifies whether a connection is open or closed, connecting , or closing.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Connection is open.
        /// </summary>
        Open,

        /// <summary>
        /// Connection is tracked but it is closed. The connection is removed after a predefined timeout.
        /// </summary>
        Closed,

        /// <summary>
        /// Connection is being connected. The initialization is in progress.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connection is currently closing but does not accept any data.
        /// </summary>
        Closing,

        /// <summary>
        /// Connection is not being tracked by the context.
        /// </summary>
        Detached
    }
}