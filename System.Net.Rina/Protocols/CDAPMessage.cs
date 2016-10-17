using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina.Protocols
{
    public enum CDAPOpcode
    {
        M_CONNECT = 0,
        M_CONNECT_R = 1,
        M_RELEASE = 2,
        M_RELEASE_R = 3,
        M_CREATE = 4,
        M_CREATE_R = 5,
        M_DELETE = 6,
        M_DELETE_R = 7,
        M_READ = 8,
        M_READ_R = 9,
        M_CANCELREAD = 10,
        M_CANCELREAD_R = 11,
        M_WRITE = 12,
        M_WRITE_R = 13,
        M_START = 14,
        M_START_R = 15,
        M_STOP = 16,
        M_STOP_R = 17
    }


    enum FlagValues
    {
        F_NO_FLAGS = 0,                         // The default value, no flags are set
        F_SYNC = 1,                             // set on READ/WRITE to request synchronous r/w
        F_RD_INCOMPLETE = 2                    // set on all but final reply to an M_READ
    }

    enum AuthenticationType
    {
        AUTH_NONE = 0,                          // No authentication
        AUTH_PASSWD = 1,                        // User name and password provided
        AUTH_SSHRSA = 2,                        // SSH RSA (version 1 or 2)
        AUTH_SSHDSA = 3                         // SSH DSA (version 2 only)
    }

    class AuthenticationValue
    {
        string authName;           // Authentication name
        string authPassword;       // Authentication password
        byte[] authOther;           // Additional authentication information
    }

    class CDAPMessage
    {
        Int32 absSyntax;            // Abstract Syntax of messages, see text.
        CDAPOpcode opCode;          // op Code.
        Int32 invokeID;         // Invoke ID, omitted if no reply desired.
        FlagValues flags;       // misc. flags
        String objClass;           // Name of the object class of objName
        String objName;            // Object name, unique in its class
        Int64 objInst;              // Unique object instance
        Object objValue;            // value of object in read/write/etc.
        Int32 result; // result of operation, 0 == success
        Int32 scope;                // scope of READ/WRITE operation
        byte[] filter;              // filter script
        AuthenticationType authMech;        // Authentication mechanism
        AuthenticationValue authValue;  // Authentication information
        String destAEInst;        // Destination AE Instance name
        String destAEName;        // Destination AE name
        String destApInst;       // Destination Application Instance name
        String destApName;        // Destination Application name
        String srcAEInst;         // Source AE Instance name
        String srcAEName;         // Source AE name
        String srcApInst;         // Source Application Instance name
        String srcApName;         // Source Application name
        String resultReason;      // further explanation of result
        Int64 version;          // For application use - RIB/class version.
    }
}

