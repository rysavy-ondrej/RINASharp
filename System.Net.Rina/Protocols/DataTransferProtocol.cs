//
//  DataTransferProtocol.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE Consortium (http://ict-pristine.eu)
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System;
using System.Runtime.Serialization;
namespace System.Net.Rina
{
	[Serializable]
	public class DataTransferProtocol : ISerializable
	{
		public ushort DestinationAddress { get; set;} 	
		public ushort SourceAddress { get; set;}  		
		public ushort DestinationCepId { get; set;}		
		public ushort SourceCepId { get; set;} 			
		public byte QosId { get; set;} 
		public byte PduType { get; set;} 
		public byte Flags { get; set;} 
		public uint SequenceNumber { get; set;} 
		private byte[] payload;
		public int Length { get { return this.payload.Length + 15; } }

		public void SetPayload(byte[] bytes)
		{
			this.payload = new byte[bytes.Length];
			Buffer.BlockCopy (bytes, 0, this.payload, 0, bytes.Length);
		}

		public byte[] GetPayload()
		{
			return this.payload;
		}
			
		public DataTransferProtocol (ushort destAddr, ushort srcAddr, ushort destCEPid, ushort srcCEPid, byte qosid, byte pdu_type, byte flags, uint seqNum, byte[] payload)
		{
			this.DestinationAddress = destAddr;
			this.SourceAddress = srcAddr;
			this.DestinationCepId = destCEPid;
			this.SourceCepId = srcCEPid;
			this.QosId = qosid;
			this.PduType = pdu_type;
			this.Flags = flags;
			this.SequenceNumber = seqNum;
			this.payload = payload;
		}

		public DataTransferProtocol ( ushort destAddr, ushort srcAddr, ushort destCEPid, ushort srcCEPid, byte pdu_type, byte[] payload)
		{
			this.DestinationAddress = destAddr;
			this.SourceAddress = srcAddr;
			this.DestinationCepId = destCEPid;
			this.SourceCepId = srcCEPid;
			this.QosId = 1;
			this.PduType = pdu_type;
			this.Flags = 0;
			this.SequenceNumber = 0;
			this.payload = payload;
		}


		public DataTransferProtocol ( ushort destAddr, ushort srcAddr, ushort destCEPid, ushort srcCEPid, byte[] payload)
		{
			this.DestinationAddress = destAddr;
			this.SourceAddress = srcAddr;
			this.DestinationCepId = destCEPid;
			this.SourceCepId = srcCEPid;
			this.QosId = 1;
			this.Flags = 0;
			this.SequenceNumber = 0;
			this.payload = payload;
		}

		public DataTransferProtocol ( ushort destAddr, ushort srcAddr, ushort destCEPid, ushort srcCEPid, byte pdu_type)
		{
			this.DestinationAddress = destAddr;
			this.SourceAddress = srcAddr;
			this.DestinationCepId = destCEPid;
			this.SourceCepId = srcCEPid;
			this.QosId = 1;
			this.PduType = pdu_type;
			this.Flags = 0;
			this.SequenceNumber = 0;
		}

		public DataTransferProtocol(byte[] bytes) {
			this.DestinationAddress = BitConverter.ToUInt16(bytes, 0); //2 bytes
			this.SourceAddress = BitConverter.ToUInt16(bytes, 2);  //2 bytes
			this.DestinationCepId =  BitConverter.ToUInt16(bytes,4);//2 bytes
			this.SourceCepId = BitConverter.ToUInt16(bytes,6); //2 bytes
			this.QosId = Buffer.GetByte(bytes,8); // 1 byte 
			this.PduType = Buffer.GetByte(bytes,9); //1 bytes
			this.Flags = Buffer.GetByte(bytes,10); //1  bytes
			this.SequenceNumber = BitConverter.ToUInt32(bytes,11); //4
			this.payload = new byte[bytes.Length-15];
			Buffer.BlockCopy (bytes, 15, this.payload, 0, this.payload.Length);
		}

		public byte[] GetBytes()
		{
			byte[] buffer = new byte[15 + this.payload.Length];

			return buffer;
		}

		#region ISerializable implementation
		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("1", this.DestinationAddress);
			info.AddValue ("2", this.SourceAddress);
			info.AddValue ("3", this.DestinationCepId);
			info.AddValue ("4", this.SourceCepId);
			info.AddValue ("5", this.QosId);
			info.AddValue ("6", this.PduType);
			info.AddValue ("7", this.Flags);
			info.AddValue ("8", this.SequenceNumber);
			info.AddValue ("9", this.payload);
		}
		protected DataTransferProtocol(SerializationInfo info, StreamingContext context)
		{
			this.DestinationAddress = info.GetUInt16 ("1");
			this.SourceAddress = info.GetUInt16 ("2");
			this.DestinationCepId = info.GetUInt16 ("3");
			this.SourceCepId = info.GetUInt16 ("4");
			this.QosId = info.GetByte ("5");
			this.PduType = info.GetByte ("6");
			this.Flags = info.GetByte ("7");
			this.SequenceNumber = info.GetUInt32 ("8");
			this.payload = (byte[])info.GetValue("9", typeof(Object));
		}
		#endregion
	}
}

