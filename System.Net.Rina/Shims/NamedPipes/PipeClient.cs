using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina.Shims.NamedPipes
{
    internal class PipeClient
    {
        private readonly NamedPipeClientStream _pipe;

        public PipeClient(NamedPipeClientStream pipe)
        {
            this._pipe = pipe;
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
            return new PipeClient(pipe);
        }

        public PipeStream Connect(Int32 timeout)
        {
            // NOTE: will throw on failure
            _pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            _pipe.ReadMode = PipeTransmissionMode.Message;

            return _pipe;
        }
    }
}
