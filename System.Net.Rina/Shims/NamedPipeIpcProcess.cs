//
// Adapted from CSNamedPipes (https://github.com/webcoyote/CSNamedPipes) by Patrick Wyatt.
//
//
//
using System;
using System.IO.Pipes;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Net.Rina.Shims
{

    // Interface for user code to receive notifications regarding pipe messages
    interface IpcCallback
    {
        void OnAsyncConnect(PipeStream pipe, out Object state);
        void OnAsyncDisconnect(PipeStream pipe, Object state);
        void OnAsyncMessage(PipeStream pipe, Byte[] data, Int32 bytes, Object state);
    }

    // Internal data associated with pipes
    struct IpcPipeData
    {
        public PipeStream pipe;
        public Object state;
        public Byte[] data;
    };

    class IpcServer
    {
        // TODO: parameterize so they can be passed by application
        public const Int32 SERVER_IN_BUFFER_SIZE = 4096;
        public const Int32 SERVER_OUT_BUFFER_SIZE = 4096;

        private readonly String m_pipename;
        private readonly IpcCallback m_callback;
        private readonly PipeSecurity m_ps;

        private bool m_running;
        private Dictionary<PipeStream, IpcPipeData> m_pipes = new Dictionary<PipeStream, IpcPipeData>();

        public IpcServer(
            String pipename,
            IpcCallback callback,
            int instances
        )
        {
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

        private void OnClientConnected(IAsyncResult result)
        {
            // Complete the client connection
            NamedPipeServerStream pipe = (NamedPipeServerStream)result.AsyncState;
            pipe.EndWaitForConnection(result);

            // Create client pipe structure
            IpcPipeData pd = new IpcPipeData();
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

        private void BeginRead(IpcPipeData pd)
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

        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            IpcPipeData pd = (IpcPipeData)result.AsyncState;
            Int32 bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
                m_callback.OnAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

    }


    class IpcClientPipe
    {
        private readonly NamedPipeClientStream m_pipe;

        public IpcClientPipe(String serverName, String pipename)
        {
            m_pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough
            );
        }

        public PipeStream Connect(Int32 timeout)
        {
            // NOTE: will throw on failure
            m_pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            m_pipe.ReadMode = PipeTransmissionMode.Message;

            return m_pipe;
        }
    }



    /// <summary>
    /// This is implementation of IpcProcess for ShimDif that employs NamedPipes for communication.
    /// </summary>
    [ShimIpc("NamedPipeIpc")]
    public class NamedPipeIpcProcess : IRinaIpc
    {
        public Address LocalAddress
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Port AllocateFlow(FlowInformation flow)
        {
            throw new NotImplementedException();
        }

        public bool DataAvailable(Port port)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DataAvailableAsync(Port port)
        {
            throw new NotImplementedException();
        }

        public void DeallocateFlow(Port port)
        {
            throw new NotImplementedException();
        }

        public void DeregisterApplication(ApplicationNamingInfo appInfo)
        {
            throw new NotImplementedException();
        }

        public PortInformationOptions GetPortInformation(Port port)
        {
            throw new NotImplementedException();
        }

        public byte[] Receive(Port port)
        {
            throw new NotImplementedException();
        }

        public void RegisterApplication(ApplicationNamingInfo appInfo, ConnectionRequestHandler reqHandler)
        {
            throw new NotImplementedException();
        }

        public int Send(Port port, byte[] buffer, int offset, int size)
        {
            throw new NotImplementedException();
        }

        public void SetBlocking(Port port, bool value)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~NamedPipeIpcProcess() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}