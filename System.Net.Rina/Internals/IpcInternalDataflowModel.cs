//
//  IpcInternalDataflowModel.cs
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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace System.Net.Rina
{


	public class BytesBlockBuffer 
	{
		private BufferBlock<byte[]> _bufferBlock = new BufferBlock<byte[]>();

		public void Enqueue(byte[] item)
		{
			this._bufferBlock.Post (item);
		}

		public byte[] Dequeue()
		{
			return this._bufferBlock.Receive ();
		}

		/// <summary>
		/// Gets the number of elements contained in the BytesBlockBuffer.
		/// </summary>
		/// <value>The count.</value>
		public int Count { get { return this._bufferBlock.Count; } }
	}
	/// This represents a source port. It is just used as the source of data within the Ipc context.
	/// New data are send by calling Send function.
	/// </summary>
	public class SourcePort : IDisposable
	{
		bool _open = true;
		ITargetBlock<byte[]> target;
		public SourcePort(ITargetBlock<byte[]> target)
		{
			this.target = target;
		}

		#region IDisposable implementation
		public void Dispose ()
		{
			this.Close ();
		}
		#endregion

		public void Send(byte[] buffer)
		{
			this.target.Post (buffer);
		}

		public void Close()
		{
			if (this._open) target.Complete ();
		}
	}

	public class SinkPort
	{
		Action<byte[]> consumer;
		public SinkPort(Action<byte[]> consumer)
		{
			this.consumer = consumer;
		}
		public async Task<Int64> ConsumeAsync(ISourceBlock<byte[]> source)
		{
			Int64 bytesProcessed = 0;
			while (await source.OutputAvailableAsync ()) {
				byte[] data = source.Receive();
				bytesProcessed += data.Length;
				consumer (data);
			}
			return bytesProcessed;
		}
	}
		
	public class ReceivingPort
	{
		Port port;
		public ReceivingPort(Port port)
		{
			this.port = port;
		}
		public void Produce(ITargetBlock<byte[]> target)
		{
			while (port.Connected) {
				var buffer = port.Ipc.Receive (port);
				target.Post (buffer);
			}
		}
	}


	/// <summary>
	/// This is consumer, it just calls Ipc send method of the associated Port.
	/// </summary>
	public class SendingPort
	{
		Port port;
		public SendingPort(Port port)
		{
			this.port = port;
		}
		public async Task<Int64> ConsumeAsync(ISourceBlock<byte[]> source)
		{
			Int64 bytesProcessed = 0;
			while (await source.OutputAvailableAsync ()) {
				byte[] data = source.Receive();
				bytesProcessed += data.Length;
				port.Ipc.Send (this.port, data);
			}
			return bytesProcessed;
		}
	}
}

