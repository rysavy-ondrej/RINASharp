using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;

namespace System.Net.Rina.Shims
{


    public enum PipeMessageType { ConnectRequest, ConnectResponse, Data }
    /// <summary>
    /// Represents message used for data communication in <see cref="PipeIpcProcess"/> DIF.
    /// </summary>
    [Serializable]
    public abstract class PipeMessage 
    {
        const byte Version = 0x1;
        /// <summary>
        /// Source address of the message.
        /// </summary>
        public Address SourceAddress;
        /// <summary>
        /// Destination address of the message.
        /// </summary>
        public Address DestinationAddress;
        /// <summary>
        /// Identification of the connection. It corresponds to Destination CepId. 
        /// </summary>
        public UInt64 DestinationCepId;


        public abstract PipeMessageType MessageType { get; }

        public void Serialize(Stream stream)
        {
            using (var wr = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                Serialize(wr, MessageType);
            }
        }

        protected virtual void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            writer.Write(getVersionTypeWord(mtype));
            SourceAddress.Serialize(writer.BaseStream);
            DestinationAddress.Serialize(writer.BaseStream);
            writer.Write(DestinationCepId);
        }


        protected virtual void Deserialize(BinaryReader reader)
        {
            SourceAddress = Address.Deserialize(reader.BaseStream);
            DestinationAddress = Address.Deserialize(reader.BaseStream);
            DestinationCepId = reader.ReadUInt64();
        }

        internal static PipeMessage Deserialize(Stream pipeStream)
        {
            using (var reader = new BinaryReader(pipeStream, Encoding.UTF8, true))
            {
                // Test if we have 
                var vertype = reader.ReadUInt16();
                var version = getVersionFromWord(vertype);
                if (version != Version) throw new NotSupportedException($"Version {version} is not supported.");
                var msgtype = getTypeFromWord(vertype);
                var message = CreateMessage(msgtype);
                message.Deserialize(reader);
                return message;   
            }           
        }

        /// <summary>
        /// Creates a default initialized message of the specified type.
        /// </summary>
        /// <param name="msgtype">A message type.</param>
        /// <returns>A new instance of message type. </returns>
        public static PipeMessage CreateMessage(PipeMessageType msgtype)
        {
            switch(msgtype)
            {
                case PipeMessageType.ConnectRequest: return new PipeConnectRequest();
                case PipeMessageType.ConnectResponse: return new PipeConnectResponse();
                case PipeMessageType.Data: return new PipeDataMessage();
                default: throw new ArgumentException($"Cannot create specified message type {msgtype}.", nameof(msgtype));
            }
        }

        protected static UInt16 getVersionTypeWord(PipeMessageType type)
        {
            var x = ((uint)Version) << 8;
            return (ushort)((uint)type | x); 
        }
        protected static PipeMessageType getTypeFromWord(UInt16 value)
        {
            var msgtype = value & 0xff;
            
            if (!Enum.IsDefined(typeof(PipeMessageType), msgtype)) throw new ArgumentException($"Given message type value {msgtype} was not recognized.", nameof(value));

            return (PipeMessageType)msgtype;
        }
        protected static int getVersionFromWord(UInt16 value)
        {
            var version = (value & 0xff00) >> 8;
            return version;
        }


        public override string ToString()
        {
            return $"PipeMessage: {MessageType}, {SourceAddress} -> {DestinationAddress}:{DestinationCepId}";
        }
    }
    /// <summary>
    /// Represents data message.
    /// </summary>
    [Serializable]
    public class PipeDataMessage : PipeMessage
    {
        public PacketDotNet.Utils.ByteArraySegment Data;

        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.Data;
            }
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write(Data.Length);
            writer.Write(Data.Bytes, Data.Offset, Data.Length);
        }

        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            var len = reader.ReadInt32();
            var bytes = reader.ReadBytes(len);
            Data = new PacketDotNet.Utils.ByteArraySegment(bytes);
        }

    }

    /// <summary>
    /// Send by PipeIpc when the new connection is requested.
    /// </summary>
    public sealed class PipeConnectRequest : PipeMessage
    {
        /// <summary>
        /// Represents a source application. 
        /// </summary>
        public string SourceApplication;
        /// <summary>
        /// Represents a name of destination application to which 
        /// the connect request should be forwarded.
        /// </summary>
        public string DestinationApplication;
        /// <summary>
        /// Represents a local connection end point identifier (CepId). 
        /// CepId is randomly generated and can be used for authentication purposes as well. 
        /// It MUST be unique in the Source IPC Scope.
        /// </summary>
        public UInt64 RequesterCepId;


        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.ConnectRequest;
            }
        }
        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write(SourceApplication);
            writer.Write(DestinationApplication);
            writer.Write(RequesterCepId);
        }
        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            SourceApplication = reader.ReadString();
            DestinationApplication = reader.ReadString();
            RequesterCepId = reader.ReadUInt64();
        }
    }

    /// <summary>
    /// Specifies a result of the connect operation. The predefined values:
    /// Accept(0), AuthenticationRequired(1), Reject(2), Fail(255). 
    /// Values in intervals 3..254 can 
    /// be used for custom result specification.
    /// </summary>
    public enum ConnectResult { Accept = 0, AuthenticationRequired = 1, Reject = 2, Fail = 255 }
    /// <summary>
    /// Message that is sent to a requester carrying the results of 
    /// the connection request operation.
    /// </summary>
    [Serializable]
    public sealed class PipeConnectResponse : PipeMessage
    {
        public ConnectResult Result;
        public UInt64 RequesterCepId;
        public UInt64 ResponderCepId;


        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.ConnectResponse;
            }
        }
        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write((byte)Result);
            writer.Write(RequesterCepId);
            writer.Write(ResponderCepId);
        }

        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Result = (ConnectResult)reader.ReadByte();
            RequesterCepId = reader.ReadUInt64();
            ResponderCepId = reader.ReadUInt64();
        }
    }
}


