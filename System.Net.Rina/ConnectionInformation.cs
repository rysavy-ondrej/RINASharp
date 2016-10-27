namespace System.Net.Rina
{
    /// <summary>
    /// This class provides information about a single connection.
    /// </summary>
    public class ConnectionInformation
    {
        /// <summary>
        /// Specifies a destination address of the current connection.
        /// </summary>
        public Address DestinationAddress;

        /// <summary>
        /// Specifies a destination application of the current connection.
        /// </summary>
        public ApplicationNamingInfo DestinationApplication;

        /// <summary>
        /// Specifies a source address of the current connection.
        /// </summary>
        public Address SourceAddress;

        /// <summary>
        /// Specifies a source application of the current connection.
        /// </summary>
        public ApplicationNamingInfo SourceApplication;
    }
}
