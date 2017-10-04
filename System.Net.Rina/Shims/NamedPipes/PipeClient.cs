using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina.Shims.NamedPipes
{
    internal enum PipeClientMode { Sync, Callback }
    /// <summary>
    /// Represents a pipe client. It can be created in two modes. In a client mode the client can be used
    /// for reading and writing messages as usual. In callback mode all incoming messages are consumed and
    /// provided to the user by calling provided callback functions.
    /// </summary>
    internal class PipeClient
    {
        public const Int32 CLIENT_IN_BUFFER_SIZE = 4096;

        public const Int32 CLIENT_OUT_BUFFER_SIZE = 4096;

        private readonly NamedPipeClientStream _pipe;
        private readonly IPipeCallback m_callback;

        string m_serverName;
        string m_pipeName;
        public string ServerName
        {
            get
            {
                return m_serverName;
            }
        }
        public string PipeName
        {
            get
            {
                return m_pipeName;
            }
        }
        public PipeClient(NamedPipeClientStream pipe)
        {
            this._pipe = pipe;
        }

        public PipeClient(NamedPipeClientStream pipe, IPipeCallback callback) : this(pipe)
        {
            this.m_callback = callback;
        }



        public PipeClientMode Mode
        {
            get
            {
                return m_callback != null ? PipeClientMode.Callback : PipeClientMode.Sync;
            }
        }
        public bool IsConnected
        {
            get
            {
                return _pipe.IsConnected;
            }
        }

        public PipeStream Stream
        {
            get
            {
                return _pipe;
            }
        }

        /// <summary>
        /// Creates a new <see cref="PipeClient"/> object for passed <paramref name="serverName"/> and <paramref name="pipename"/>.
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="pipename"></param>
        /// <returns></returns>
        public static PipeClient Create(String serverName, String pipename)
        {
            Trace.TraceInformation($"PipeClient.Create(serverName:{serverName},pipename:{pipename})");
            if (serverName == null) throw new ArgumentNullException(nameof(serverName));
            if (pipename == null) throw new ArgumentNullException(nameof(pipename));

            var pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            return new PipeClient(pipe) { m_pipeName = pipename, m_serverName = serverName };
        }
        /// <summary>
        /// Creates a new <see cref="PipeClient"/> object in a callback mode
        /// for passed <paramref name="serverName"/> and <paramref name="pipename"/>.
        /// All incoming messages will be passed to callback provided by the user of the <see cref="PipeClient"/>.
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="pipename"></param>
        /// <param name="callback">Implementation of <see cref="IPipeCallback"/> interface that will be used to consume incoming messages.</param>
        /// <returns></returns>
        public static PipeClient Create(String serverName, String pipename, IPipeCallback callback)
        {
            Trace.TraceInformation($"PipeClient.Create(serverName:{serverName},pipename:{pipename})");
            if (serverName == null) throw new ArgumentNullException(nameof(serverName));
            if (pipename == null) throw new ArgumentNullException(nameof(pipename));

            var pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            return new PipeClient(pipe, callback) { m_pipeName = pipename, m_serverName = serverName };
        }

        public PipeStream Connect(Int32 timeout)
        {
            // NOTE: will throw on failure
            _pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            _pipe.ReadMode = PipeTransmissionMode.Message;

            if (m_callback != null)
                OnClientConnected(_pipe);

            return _pipe;
        }

        private void BeginRead(PipeServer.PipeData pd)
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
                m_callback.OnPipeAsyncDisconnect(pd.pipe, pd.state);
            }
        }
        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            PipeServer.PipeData pd = (PipeServer.PipeData)result.AsyncState;
            int bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
                m_callback.OnPipeAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

        private void OnClientConnected(PipeStream pipe)
        {
            // Create client pipe structure
            PipeServer.PipeData pd = new PipeServer.PipeData();
            pd.pipe = pipe;
            pd.state = null;
            pd.data = new Byte[CLIENT_IN_BUFFER_SIZE];

            // Alert server that client connection exists
            m_callback.OnPipeAsyncConnect(pipe, out pd.state);

            // Accept messages
            BeginRead(pd);
        }
    }
}
