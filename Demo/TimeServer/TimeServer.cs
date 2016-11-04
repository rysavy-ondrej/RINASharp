using ConsoleToolkit.CommandLineInterpretation.ConfigurationAttributes;
using ConsoleToolkit.ConsoleIO;
using System;
using System.IO;
using System.Net.Rina;
using System.Threading;
using System.Threading.Tasks;

namespace TimeService
{

    [Command]
    [Description("Executes a TimeServer instance.")]
    class TimeServer
    {
        [Option("name", shortName: "n")]
        [Description("A name of the server.")]
        public string ServerName { get; set; }

        [Option("address", shortName: "a")]
        [Description("Local address of the server in the underlying DIF.")]
        public string Address { get; set; }

        [Option("shimDif", shortName: "d")]
        [Description("Specifies underlying ShimDIF to use.")]
        public string ShimDif { get; set; }

        [Option("help", shortName: "h", ShortCircuit = true)]
        [Description("Display this help text.")]
        public bool Help { get; set; }


        /// <summary>
        /// Creates a new instance of <see cref="TimeServer"/>.
        /// </summary>
        public TimeServer()
        {
            ServerName = "TimeServer";
            Address = "Dublin";
            ShimDif = IpcProcessType.WcfShim.ToString();
        }

        /// <summary>
        /// Executed after all arguments were parsed. The method initializes and runs <see cref="TimeServer"/>.
        /// </summary>
        /// <param name="console"></param>
        /// <param name="error"></param>
        [CommandHandler]
        public void Handler(IConsoleAdapter console, IErrorAdapter error)
        {
            var shimType = IpcProcessType.WcfShim;
            Enum.TryParse<IpcProcessType>(ShimDif, true, out shimType);

            using (var host = new IpcHost())
            {
                var shimIpcProcess = IpcProcessFactory.CreateProcess(host, shimType, Address);
                shimIpcProcess.RegisterApplication(new ApplicationNamingInfo(ServerName, "1", "TimeServiceProtocol", "1"), applicationRequestHandler);

                var evh = new EventWaitHandle(false, EventResetMode.AutoReset);
                System.Console.CancelKeyPress += new ConsoleCancelEventHandler((x, a) => evh.Set());
                evh.WaitOne();
            }
        }

        /// <summary>
        /// This method decides whether to accept or reject the request. 
        /// </summary>
        private ConnectionRequestResult applicationRequestHandler(IRinaIpc context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler)
        {
            Console.WriteLine($"FlowInfo:{flowInformation.SourceApplication}@{flowInformation.SourceAddress} <-> {flowInformation.DestinationApplication}@{flowInformation.DestinationAddress}");
            acceptFlowHandler = this.acceptFlowHandler;
            return ConnectionRequestResult.Accept;
        }
        /// <summary>
        /// This method is called for creating a new instance for the server and executing its worker thread.
        /// </summary>
        private Task acceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port)
        {
            return Task.Run(() =>
            {
                port.Blocking = true;    
                using (var stream = new IpcChannelStream(port))
                using (var tr = new StreamReader(stream))
                using (var tw = new StreamWriter(stream))
                {
                    var line = tr.ReadLine();
                    switch ((line ?? String.Empty).Trim())
                    {
                        case "DateTime.Now": tw.WriteLine($"OK: {DateTime.Now}"); break;
                        default: tw.WriteLine("ERROR: Invalid request!"); break;
                    }
                    //port.Shutdown(Timeout.InfiniteTimeSpan);
                }
            });

        }
    }
}
