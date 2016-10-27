// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// BufferBlock.cs
//
//
// A propagator block that provides support for unbounded and bounded FIFO buffers.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Security;
using System.Threading.Tasks.Dataflow.Internal;
using System.Diagnostics.CodeAnalysis;

namespace System.Threading.Tasks.Dataflow
{
    /// <summary>Provides a buffer for storing data.</summary>
    /// <typeparam name="T">Specifies the type of the data buffered by this dataflow block.</typeparam>
    public sealed class ReceiveBufferBlock<T> : 
        ITargetBlock<ArraySegment<T>>, IDataflowBlock
    {

        BufferBlock<ArraySegment<T>> m_tail = new BufferBlock<ArraySegment<T>>();
        ArraySegment<T>? m_head = null;
                                
        public int TryRead(T[] buffer, int offset, int size)
        {
            // if no data are in receive buffer then load some message:
            if (m_head == null)
            {
                try
                {
                    ArraySegment<T> item;
                    if (m_tail.TryReceive(out item))
                    {
                        m_head = item;    
                    }
                    return 0;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }
            return readInternal(buffer, offset, size);
        }

        /// <summary>
        /// Reads up to <paramref name="size"/> elements from the current buffer. Note that if there
        /// are any items at least one will be read.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns>
        /// A number of elements supplied by this function. If no data is available it returns 0. In
        /// case of some error occurred it returns -1.
        /// </returns>
        public int Read(T[] buffer, int offset, int size, TimeSpan timeout)
        {
            // if no data are in receive buffer then load some message:
            if (m_head == null)
            {
                try
                {
                    m_head = m_tail.Receive(timeout);

                }
                catch (TimeoutException)
                {
                    return 0;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }
            return readInternal(buffer, offset, size);
        }

        private int readInternal(T[] buffer, int offset, int size)
        {
            var itemsReceived = 0;
            // Read bytes from the received buffer:
            if (size >= m_tail?.Count)
            {  // Consume all bytes
                itemsReceived = m_head.Value.Count;
                Buffer.BlockCopy(m_head.Value.Array, m_head.Value.Offset, buffer, offset, itemsReceived);
                m_head = null;
            }
            else
            {   // Copy only part of ReceiveBuffer, adjusting the rest
                itemsReceived = size;
                Buffer.BlockCopy(m_head.Value.Array, m_head.Value.Offset, buffer, offset, itemsReceived);
                m_head = new ArraySegment<T>(m_head.Value.Array, m_head.Value.Offset + itemsReceived, m_head.Value.Count - itemsReceived);
            }
            m_itemsAvailable -= itemsReceived;
            return itemsReceived;
        }

        public void Complete()
        {
            m_tail.Complete();
        }

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)m_tail).Fault(exception);
        }

        private int m_itemsAvailable;
        public int ItemsAvailable
        {
            get
            {
                return m_itemsAvailable;
            }
        }

        public Task Completion
        {
            get
            {
                return m_tail.Completion;
            }
        }

        internal Task<bool> OutputAvailableAsync(CancellationToken ct)
        {
            if (m_head != null) return Task.Run(() => true);
            else return m_tail.OutputAvailableAsync(ct);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, ArraySegment<T> messageValue, ISourceBlock<ArraySegment<T>> source, bool consumeToAccept)
        {
            var status = ((ITargetBlock<ArraySegment<T>>)m_tail).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
            if (status == DataflowMessageStatus.Accepted)
            {
                m_itemsAvailable += messageValue.Count;
            }            
            return status;
        }
    }
}