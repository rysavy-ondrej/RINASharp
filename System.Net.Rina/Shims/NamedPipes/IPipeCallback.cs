using System.IO.Pipes;

namespace System.Net.Rina.Shims.NamedPipes
{
    // Interface for user code to receive notifications regarding pipe messages
    internal interface IPipeCallback
    {
        void OnAsyncConnect(PipeStream pipe, out Object state);

        void OnAsyncDisconnect(PipeStream pipe, Object state);

        void OnAsyncMessage(PipeStream pipe, Byte[] data, Int32 bytes, Object state);
    }
}
