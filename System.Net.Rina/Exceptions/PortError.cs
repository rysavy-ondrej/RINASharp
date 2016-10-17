using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    /// <summary>
    /// Defines port operation error constants.
    /// </summary>
    public enum PortError : int
    {
        /// <summary>
        ///  The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        ///  The socket has an error.
        /// </summary>
        PortError = (-1),
        /// <summary>
        /// A blocking socket call was canceled.
        /// </summary>
        Interrupted = (10000 + 4),

        /// <summary>
        /// Permission denied.
        /// </summary>
        AccessDenied = (10000 + 13),
        /// <summary>
        /// Bad address.
        /// </summary>
        Fault = (10000 + 14),
        /// <summary>
        /// Invalid argument.
        /// </summary>
        InvalidArgument = (10000 + 22),
        /// <summary>
        /// Too many open files.
        /// </summary>
        TooManyOpenSockets = (10000 + 24),
        /// Resource temporarily unavailable.
        /// </summary>
        WouldBlock = (10000 + 35),
        /// <summary>
        /// Operation now in progress.
        /// </summary>
        InProgress = (10000 + 36),
        /// <summary>
        /// Operation already in progress.
        /// </summary>
        AlreadyInProgress = (10000 + 37),
        /// <summary>
        /// Socket operation on nonsocket.
        /// </summary>
        NotSocket = (10000 + 38),
        /// <summary>
        /// Destination address required.
        /// </summary>
        DestinationAddressRequired = (10000 + 39),
        /// <summary>
        /// Message too long.
        /// </summary>
        MessageSize = (10000 + 40),
        ProtocolType = (10000 + 41),
        /// <summary>
        /// Bad protocol option.
        /// </summary>
        ProtocolOption = (10000 + 42),
        /// <summary>
        /// Protocol not supported.
        /// </summary>
        ProtocolNotSupported = (10000 + 43),
        /// <summary>
        /// Socket type not supported.
        /// </summary>
        SocketNotSupported = (10000 + 44),
        /// <summary>
        /// Operation not supported.
        /// </summary>
        OperationNotSupported = (10000 + 45),
        /// <summary>
        /// Protocol family not supported.
        /// </summary>
        ProtocolFamilyNotSupported = (10000 + 46),
        /// <summary>
        /// Address family not supported by protocol family.
        /// </summary>
        AddressFamilyNotSupported = (10000 + 47),
        /// <summary>
        /// Address already in use.
        /// </summary>
        AddressAlreadyInUse = (10000 + 48),
        /// <summary>
        /// Cannot assign requested address.
        /// </summary>
        AddressNotAvailable = (10000 + 49),
        /// <summary>
        /// Network is down.
        /// </summary>
        NetworkDown = (10000 + 50),
        /// <summary>
        /// Network is unreachable.
        /// </summary>
        NetworkUnreachable = (10000 + 51),
        /// <summary>
        /// Network dropped connection on reset.
        /// </summary>
        NetworkReset = (10000 + 52),
        /// <summary>
        /// Software caused connection to abort.
        /// </summary>
        ConnectionAborted = (10000 + 53),
        /// <summary>
        /// Connection reset by peer.
        /// </summary>
        ConnectionReset = (10000 + 54),
        /// <summary>
        /// No buffer space available.
        /// </summary>
        NoBufferSpaceAvailable = (10000 + 55),
        //WSAENOBUFS
        /// <devdoc>
        /// <para>
        /// Socket is already connected.
        /// </para>
        /// </devdoc>
        IsConnected = (10000 + 56),
        /// <summary>
        /// Socket is not connected.
        /// </summary>
        NotConnected = (10000 + 57),
        /// <summary>
        /// Cannot send after socket shutdown.
        /// </summary>
        Shutdown = (10000 + 58),
        /// <summary>
        /// Connection timed out.
        /// </summary>
        TimedOut = (10000 + 60),
        /// <summary>
        ///  Connection refused.
        /// </summary>
        ConnectionRefused = (10000 + 61),
        /// <summary>
        /// Host is down.
        /// </summary>
        HostDown = (10000 + 64),
        /// <summary>
        ///  No route to host.
        /// </summary>
        HostUnreachable = (10000 + 65),
        /// <summary>
        /// Too many processes.
        /// </summary>
        ProcessLimit = (10000 + 67),
        /// <summary>
        /// Network subsystem is unavailable.
        /// </summary>
        SystemNotReady = (10000 + 91),
        /// <summary>
        /// Specified version of RINA is not supported.
        /// </summary>
        VersionNotSupported = (10000 + 92),
        /// <summary>
        /// Successful startup not yet performed.
        /// </summary>
        NotInitialized = (10000 + 93),
        /// <summary>
        /// Graceful shutdown in progress.
        /// </summary>
        Disconnecting = (10000 + 101),
        //WSAEDISCON

        TypeNotFound = (10000 + 109),
        /// <summary>
        /// Specified host not found.
        /// </summary>
        HostNotFound = (10000 + 1001),
        /// <summary>
        /// 
        /// </summary>
        TryAgain = (10000 + 1002),
        /// <summary>
        /// This is a nonrecoverable error (Non recoverable errors, FORMERR, REFUSED, NOTIMP).
        /// </summary>
        NoRecovery = (10000 + 1003),
        /// <summary>
        /// No data record of requested type.
        /// </summary>
        NoData = (10000 + 1004),     
    }
}
