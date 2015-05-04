using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Rina;
using System.Net.Rina.Naming;
using System.Net.Rina.Shims;
using System.Diagnostics;
using System.Threading;
namespace TimeService
{



    /// <summary>
    /// This is a simple program that registers itself as a time server in the Time Services DIF. 
    /// </summary>
    class Program
    {

        static WcfServiceIpcProcess ipc;
        // Establish an event handler to process key press events.
        protected static void consoleCancelEventHandler(object sender, ConsoleCancelEventArgs args)
        {
            ipc.Dispose();
        }
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(consoleCancelEventHandler);

            var serviceSide = args.Length > 0 ? args[0] : String.Empty;
            switch (serviceSide)
            {
                case "server": /// Server side
                    {
                        using (ipc = WcfServiceIpcProcess.Create("server"))
                        {
                            if (ipc == null) return;
                            ipc.RegisterApplication(new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "V1"), applicationRequestHandler);
                            ipc.Worker.Join();
                        }
                        break;                        
                    }
                case "client": /// Client side:
                    {
                        var r = new Random();
                        using (ipc = WcfServiceIpcProcess.Create("client"+Process.GetCurrentProcess().Id.ToString()))
                        {
                            if (ipc == null) return;
                            var remoteAddress = Address.FromWinIpcPort("server");
                            var flowInformation = new FlowInformation()
                            {
                                SourceApplication = new ApplicationNamingInfo("TimeClient", "1", "TimeServiceProtocol", "V1"),
                                DestinationApplication = new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "V1"),
                                SourceAddress = ipc.LocalAddress,
                                DestinationAddress = remoteAddress
                            };

                            while (true)
                            {
                                var port = ipc.AllocateFlow(flowInformation);
                                if (port == null)
                                    break;
                                port.Blocking = true;

                                var cmdBytes = System.Text.ASCIIEncoding.ASCII.GetBytes("DateTime.Now\n");
                                ipc.Send(port, cmdBytes, 0, cmdBytes.Length);
                                var answerBuffer = new byte[1024];
                                var answerLength = ipc.Receive(port, answerBuffer, 0, answerBuffer.Length);
                                var answerString = System.Text.ASCIIEncoding.ASCII.GetString(answerBuffer, 0, answerLength);
                                System.Console.WriteLine("Current remote time is: {0}", answerString);
                                
                                Thread.Sleep(r.Next(500));
                                ipc.DeallocateFlow(port);
                            }                            
                        }
                        break; 
                    }
                default:
                    Console.WriteLine("To start server type 'TimeService server'");
                    Console.WriteLine("To start client type 'TimeService client'");
                    break;                    
            }
        }

        /// <summary>
        /// This method decides whether to accept or reject the request. 
        /// </summary>
        private static ConnectionRequestResult applicationRequestHandler(IRinaIpc context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler)
        {
            acceptFlowHandler = Program.acceptFlowHandler;
            return ConnectionRequestResult.Accept;
        }
        /// <summary>
        /// This method is called for creating a new instance for the server and executing its worker thread.
        /// </summary>
        private static void acceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port)
        {            
            var s = new TimeServerInstance(context, port);
            s.Start();                    
        }

        internal class TimeServerInstance
        {
            IRinaIpc _context;
            Port _port;
            Thread _thread;
            internal TimeServerInstance(IRinaIpc context, Port port)
            {
                this._context = context;
                this._port = port;
            }

            public Thread Start()
            {
                _thread = new Thread(worker);
                _thread.IsBackground = true;
                _thread.Start();
                return _thread;
            }

            private void worker()
            {
                _port.Blocking = true;
                using (var stream = new IpcChannelStream(this._port))                    
                using (var tr = new StreamReader(stream))
                using (var tw = new StreamWriter(stream))
                {
                    // A line is defined as a sequence of characters followed by a line feed ("\n"), a carriage return ("\r") or a carriage return immediately followed by a line feed("\r\n").
                    // The string that is returned does not contain the terminating carriage return or line feed. The returned value is a null reference if the end of the input stream is reached.
                    var line = tr.ReadLine();
                    Console.WriteLine("Received line: '{0}'", line);
                    switch ((line??String.Empty).Trim())
                    {
                        case "DateTime.Now": tw.WriteLine("{0}", DateTime.Now); break;
                        default: tw.WriteLine("ERROR: Invalid request!"); break;
                    }
                }
                // Close the connection by deallocating flow, is it correct?
                this._context.DeallocateFlow(this._port);
            }
        }
    }
}
