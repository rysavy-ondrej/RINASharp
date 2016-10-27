namespace System.Net.Rina
{
    public enum ConnectionType
    {
        /// <summary>
        /// Stream oriented connection.
        /// </summary>
        Stream = 1,

        /// <summary>
        /// Datagram (block) oriented connection type.
        /// </summary>
        Dgram = 2,

        /// <summary>
        /// Raw connection type.
        /// </summary>
        Raw = 3,

        /// <summary>
        /// Reliably delivered message oriented connection type.
        /// </summary>
        Rdm = 4,

        /// <summary>
        /// Unknown type.
        /// </summary>
        Unknown = -1,
    }
}
