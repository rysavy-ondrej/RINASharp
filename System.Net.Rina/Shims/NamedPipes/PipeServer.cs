using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace System.Net.Rina.Shims.NamedPipes
{
    internal class PipeServer
    {
        // TODO: parameterize so they can be passed by application
        public const Int32 SERVER_IN_BUFFER_SIZE = 4096;

        public const Int32 SERVER_OUT_BUFFER_SIZE = 4096;

        private readonly IPipeCallback m_callback;

        private readonly String m_pipename;

        private readonly PipeSecurity m_ps;

        private Dictionary<PipeStream, PipeData> m_pipes = new Dictionary<PipeStream, PipeData>();

        private bool m_running;

        public PipeServer(
                    String pipename,
                    IPipeCallback callback,
                    int instances
                )
        {
            Trace.TraceInformation($"new PipeServer(pipename:{pipename},instances:{instances})");

            Debug.Assert(!m_running);
            m_running = true;

            // Save parameters for next new pipe
            m_pipename = pipename;
            m_callback = callback;

            // Provide full access to the current user so more pipe instances can be created
            m_ps = new PipeSecurity();
            m_ps.AddAccessRule(
                new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow)
            );
            m_ps.AddAccessRule(
                new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow
                )
            );

            // Start accepting connections
            for (int i = 0; i < instances; ++i)
                IpcServerPipeCreate();
        }

        public void IpcServerStop()
        {
            // Close all pipes asynchronously
            lock (m_pipes)
            {
                m_running = false;
                foreach (var pipe in m_pipes.Keys)
                    pipe.Close();
            }

            // Wait for all pipes to close
            for (;;)
            {
                int count;
                lock (m_pipes)
                {
                    count = m_pipes.Count;
                }
                if (count == 0)
                    break;
                Thread.Sleep(5);
            }
        }

        private void BeginRead(PipeData pd)
        {
            // Asynchronously read a request from the client
            bool isConnected = pd.pipe.IsConnected;
            if (isConnected)
            {
                try
                {
                    pd.pipe.BeginRead(pd.data, 0, pd.data.Length, OnAsyncMessage, pd);
                }
                catch (Exception)
                {
                    isConnected = false;
                }
            }

            if (!isConnected)
            {
                pd.pipe.Close();
                m_callback.OnAsyncDisconnect(pd.pipe, pd.state);
                lock (m_pipes)
                {
                    bool removed = m_pipes.Remove(pd.pipe);
                    Debug.Assert(removed);
                }
            }
        }

        private void IpcServerPipeCreate()
        {
            // Create message-mode pipe to simplify message transition
            // Assume all messages will be smaller than the pipe buffer sizes
            NamedPipeServerStream pipe = new NamedPipeServerStream(
                m_pipename,
                PipeDirection.InOut,
                -1,     // maximum instances
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                SERVER_IN_BUFFER_SIZE,
                SERVER_OUT_BUFFER_SIZE,
                m_ps
            );

            // Asynchronously accept a client connection
            pipe.BeginWaitForConnection(OnClientConnected, pipe);
        }

        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            PipeData pd = (PipeData)result.AsyncState;
            Int32 bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
                m_callback.OnAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

        private void OnClientConnected(IAsyncResult result)
        {
            // Complete the client connection
            NamedPipeServerStream pipe = (NamedPipeServerStream)result.AsyncState;
            pipe.EndWaitForConnection(result);

            // Create client pipe structure
            PipeData pd = new PipeData();
            pd.pipe = pipe;
            pd.state = null;
            pd.data = new Byte[SERVER_IN_BUFFER_SIZE];

            // Add connection to connection list
            bool running;
            lock (m_pipes)
            {
                running = m_running;
                if (running)
                    m_pipes.Add(pd.pipe, pd);
            }

            // If server is still running
            if (running)
            {
                // Prepare for next connection
                IpcServerPipeCreate();

                // Alert server that client connection exists
                m_callback.OnAsyncConnect(pipe, out pd.state);

                // Accept messages
                BeginRead(pd);
            }
            else
            {
                pipe.Close();
            }
        }

        // Internal data associated with pipes
        internal struct PipeData
        {
            public Byte[] data;
            public PipeStream pipe;
            public Object state;
        };
    }
}
