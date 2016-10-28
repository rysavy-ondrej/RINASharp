using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

namespace System.Net.Rina.Shims.NamedPipes
{


    /// <summary>
    /// Specifies a result of the connect operation. The predefined values:
    /// Accept(0), AuthenticationRequired(1), Reject(2), Fail(255). 
    /// Values in intervals 3..254 can 
    /// be used for custom result specification.
    /// </summary>
    public enum ConnectResult {
        /// <summary>
        /// Connection was accepted by the application.
        /// </summary>
        Accepted = 0,
        /// <summary>
        /// Application requires authentication.
        /// </summary>
        AuthenticationRequired = 1,
        /// <summary>
        /// Application rejected the connection.
        /// </summary>
        Rejected = 2,
        /// <summary>
        /// Application was not found.
        /// </summary>
        NotFound,
        Fail = 255 }

    /// <summary>
    /// Specifies a way used for closing the connection.
    /// </summary>
    public enum DisconnectFlags : int
    {
        /// <summary>
        /// Connection is aborted that means all pending data should be discarded.
        /// </summary>
        Abort = 1,
        /// <summary>
        /// Connection will be closed gracefully. Pending data will be processed and
        /// then the connection will close.
        /// </summary>
        Gracefull = 2,
        /// <summary>
        /// Indicates that we have no more data to send for this connection.
        /// </summary>
        Close = 3
    };

    public enum PipeMessageType { ConnectRequest, ConnectResponse, DisconnectRequest, DisconnectResponse, Data }
    /// <summary>
    /// Send by PipeIpc when the new connection is requested.
    /// </summary>
    public sealed class PipeConnectRequest : PipeMessage
    {
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

        /// <summary>
        /// Represents a source application. 
        /// </summary>
        public string SourceApplication;
        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.ConnectRequest;
            }
        }
        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            SourceApplication = reader.ReadString();
            DestinationApplication = reader.ReadString();
            RequesterCepId = reader.ReadUInt64();
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write(SourceApplication);
            writer.Write(DestinationApplication);
            writer.Write(RequesterCepId);
        }
    }

    /// <summary>
    /// Message that is sent to a requester carrying the results of 
    /// the connection request operation.
    /// </summary>
    [Serializable]
    public sealed class PipeConnectResponse : PipeMessage
    {
        public UInt64 RequesterCepId;
        public UInt64 ResponderCepId;
        public ConnectResult Result;
        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.ConnectResponse;
            }
        }
        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Result = (ConnectResult)reader.ReadByte();
            RequesterCepId = reader.ReadUInt64();
            ResponderCepId = reader.ReadUInt64();
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write((byte)Result);
            writer.Write(RequesterCepId);
            writer.Write(ResponderCepId);
        }
    }

    /// <summary>
    /// Represents data message.
    /// </summary>
    [Serializable]
    public class PipeDataMessage : PipeMessage
    {
        public ArraySegment<byte> Data;

        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.Data;
            }
        }

        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            var len = reader.ReadInt32();
            var bytes = reader.ReadBytes(len);
            Data = new ArraySegment<byte>(bytes);
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write(Data.Count);
            writer.Write(Data.Array, Data.Offset, Data.Count);
        }
    }

    /// <summary>
    /// Disconnect message. It specifies the way the disconnection will be performed.
    /// </summary>
    /// <remarks>
    /// Abortive shutdown sequence - all incoming messages will be discarded.
    /// Graceful shutdown, delaying return until either shutdown sequence completes or a specified time interval elapses.
    /// 
    /// </remarks>
    public sealed class PipeDisconnectRequest : PipeMessage
    {

        public DisconnectFlags Flags;
        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.DisconnectRequest;
            }
        }
        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Flags = (DisconnectFlags)reader.ReadInt32();
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write((int)Flags);
        }
    }

    public sealed class PipeDisconnectResponse : PipeMessage
    {
        public DisconnectFlags Flags;
        public override PipeMessageType MessageType
        {
            get
            {
                return PipeMessageType.DisconnectResponse;
            }
        }
        protected override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Flags = (DisconnectFlags)reader.ReadInt32();
        }

        protected override void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            base.Serialize(writer, mtype);
            writer.Write((int)Flags);
        }
    }

    /// <summary>
    /// Represents message used for data communication in <see cref="PipeIpcProcess"/> DIF.
    /// </summary>
    [Serializable]
    public abstract class PipeMessage 
    {
        /// <summary>
        /// Destination address of the message.
        /// </summary>
        public Address DestinationAddress;

        /// <summary>
        /// Identification of the connection. It corresponds to Destination CepId. 
        /// </summary>
        public UInt64 DestinationCepId;

        /// <summary>
        /// Source address of the message.
        /// </summary>
        public Address SourceAddress;

        const byte Version = 0x1;
        public abstract PipeMessageType MessageType { get; }

        /// <summary>
        /// Creates a default initialized message of the specified type.
        /// </summary>
        /// <param name="msgtype">A message type.</param>
        /// <returns>A new instance of message type. </returns>
        public static PipeMessage CreateMessage(PipeMessageType msgtype)
        {
            switch (msgtype)
            {
                case PipeMessageType.ConnectRequest: return new PipeConnectRequest();
                case PipeMessageType.ConnectResponse: return new PipeConnectResponse();
                case PipeMessageType.DisconnectRequest: return new PipeDisconnectRequest();
                case PipeMessageType.DisconnectResponse: return new PipeDisconnectResponse();
                case PipeMessageType.Data: return new PipeDataMessage();
                default: throw new ArgumentException($"Cannot create specified message type {msgtype}.", nameof(msgtype));
            }
        }

        public void Serialize(Stream stream)
        {
            using (var wr = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                Serialize(wr, MessageType);
            }
        }

        public override string ToString()
        {
            return $"PipeMessage: {MessageType}, {SourceAddress} -> {DestinationAddress}:{DestinationCepId}";
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

        protected static UInt16 getVersionTypeWord(PipeMessageType type)
        {
            var x = ((uint)Version) << 8;
            return (ushort)((uint)type | x);
        }

        protected virtual void Deserialize(BinaryReader reader)
        {
            SourceAddress = Address.Deserialize(reader.BaseStream);
            DestinationAddress = Address.Deserialize(reader.BaseStream);
            DestinationCepId = reader.ReadUInt64();
        }

        protected virtual void Serialize(BinaryWriter writer, PipeMessageType mtype)
        {
            writer.Write(getVersionTypeWord(mtype));
            SourceAddress.Serialize(writer.BaseStream);
            DestinationAddress.Serialize(writer.BaseStream);
            writer.Write(DestinationCepId);
        }
    }
    internal static class PipeMessageEncoder
    {
        internal static PipeMessage ReadMessage(byte[] data, int offset, int bytes)
        {            
            using (var ms = new MemoryStream(data, 0, bytes))
            {
                try
                {
                    var msg = PipeMessage.Deserialize(ms);
                    Trace.TraceInformation($"PipeMessageEncoder.ReadMessage: Message {msg.MessageType}, len = {bytes}B: {BitConverter.ToString(data, offset, bytes)}");
                    return msg;
                }
                catch (Exception e)
                {
                    Trace.TraceError($"{nameof(ReadMessage)}: Error when deserializing message: {e.Message}");
                    return null;
                }
            }
        }

        internal static PipeMessage ReadMessage(PipeStream pipeStream)
        {
            var buffer = new byte[128];
            using (var ms = new MemoryStream())
            {
                do
                {
                    var len = pipeStream.Read(buffer, 0, buffer.Length);
                    if (len < 0) break;
                    ms.Write(buffer, 0, len);
                }
                while (!pipeStream.IsMessageComplete);
                var count = (int)ms.Position;
                ms.Position = 0;                
                try
                {
                    var msg = PipeMessage.Deserialize(ms);
                    Trace.TraceInformation($"PipeMessageEncoder.ReadMessage: Message {msg.MessageType}, len = {count}B: {BitConverter.ToString(ms.GetBuffer(), 0, count)}");
                    return msg;
                }
                catch (Exception e)
                {
                    Trace.TraceError($"{nameof(ReadMessage)}: Error when deserializing message: {e.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Writes a <see cref="PipeMessage<"/> to a specified <see cref="PipeStream"/>.
        /// It uses intermediate buffer represented by <see cref="MemoryStream"/> 
        /// because each calling of <see cref="PipeStream.Write(byte[], int, int)"/> 
        /// creates a new message.
        /// </summary>
        /// <param name="pipeStream">A <see cref="PipeStream"/> to which the message is written.</param>
        /// <param name="message">An object derived from <see cref="PipeMessage"/> representing the message.</param>
        /// <returns>The number of bytes written to the <see cref="PipeStream"/> object.</returns>
        internal static int WriteMessage(PipeMessage message, Stream pipeStream)
        {
            using (var ms = new MemoryStream())
            {
                message.Serialize(ms);
                var buf = ms.GetBuffer();
                var count = (int)ms.Position;
                Trace.TraceInformation($"PipeMessageEncoder.WriteMessage: Message {message.MessageType}, len={count}B: {BitConverter.ToString(buf, 0, count)}");
                pipeStream.Write(buf, 0, count);
                return count;
            }
        }

        internal static byte[] WriteMessage(PipeMessage message)
        {
            using (var ms = new MemoryStream())
            {
                message.Serialize(ms);
                return ms.ToArray();
            }
        }
    }
}


