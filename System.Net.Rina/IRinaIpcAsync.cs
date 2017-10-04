using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    /// <summary>
    /// This interface represents basic asynchronous IPC API.
    /// </summary>
    internal interface IRinaIpcAsync
    {
        /// <summary>
        /// Aborts the specified connection.
        /// </summary>
        /// <remarks>
        /// The actions and events related to connection abort are as follows:
        /// 1. The client calls Abort that causes sending DisconnectRequest message of type Abort.
        /// 2. When the server receives DisconnectRequest message of type Abort, it should discard
        ///    all messages and may optionally send DisconnectRespose message
        /// 3. The client will not process any other messages except DisconnectResponse message.
        /// </remarks>
        /// <param name="port">Port descriptor.</param>
        /// <param name="waitForResponse">If <see langword="true"/> than the method waits for <see cref="PipeDisconnectResponse"/> message.</param>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>Task that finishes when Abort is completed. It status in <see cref="IpcError"/> value.</returns>
        Task<IpcError> AbortAsync(Port port, bool waitForResponse, CancellationToken ct);
    }
}