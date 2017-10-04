using System.IO.Pipes;

namespace System.Net.Rina.Shims.NamedPipes
{
    // Interface for user code to receive notifications regarding pipe messages
    internal interface IPipeCallback
    {
        void OnPipeAsyncConnect(PipeStream pipe, out Object state);

        void OnPipeAsyncDisconnect(PipeStream pipe, Object state);

        void OnPipeAsyncMessage(PipeStream pipe, Byte[] data, Int32 bytes, Object state);
    }
}
