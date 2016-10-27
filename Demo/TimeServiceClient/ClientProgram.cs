//
//  ClientProgram.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2016 PRISTINE
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using ConsoleToolkit;
using ConsoleToolkit.ApplicationStyles;
using ConsoleToolkit.CommandLineInterpretation.ConfigurationAttributes;
using ConsoleToolkit.ConsoleIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Rina;
using System.Net.Rina.Shims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimeServiceClient
{

    /// <summary>
    /// Implements a simple program that prints time information by querying TimeService.
    /// </summary>
    class ClientProgram : ConsoleApplication
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(System.Console.Out));
            Trace.AutoFlush = true;
            Toolkit.Execute<ClientProgram>(args);
        }

        protected override void Initialise()
        {
            base.HelpOption<TimeClient>(o => o.Help);
            base.Initialise();
        }
    }

    [Command]
    [Description("Runs an instance of TimeClient - a simple tool that reads actual time from the server.")]
    class TimeClient
    {

        [Option("name", shortName: "n")]
        [Description("A name of the client.")]
        public string ClientName { get; set; }

        [Option("address", shortName: "a")]
        public string LocalAddress { get; set; }

        [Option("shimDif", shortName: "d")]
        [Description("Specifies underlying ShimDIF to use. Default is WcfShim.")]
        public string ShimDif { get; set; }

        [Option("help", shortName: "h", ShortCircuit = true)]
        [Description("Display this help text.")]
        public bool Help { get; set; }

        [Positional]
        [Description("Server address in form of: rina://dif-nametype/server-node/apps/app-name")]
        public string ServerAddress { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="TimeServer"/>.
        /// </summary>
        public TimeClient()
        {
            ClientName = "TimeClient";
            LocalAddress = "Brno";
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


            // parse server address:
            var serverUri = new Uri(ServerAddress);
            var difName = serverUri.GetDifName();
            IpcProcessType difType;
            Enum.TryParse<IpcProcessType>(difName, true, out difType);
            var ipcAddress = serverUri.GetIpcAddress();
            var appName = serverUri.GetAppName();
            Trace.TraceInformation("---------------------------------------");
            Trace.TraceInformation($"RINA-URL:    {serverUri.AbsoluteUri}");
            Trace.TraceInformation($"SHIM:        {difName} ({difType})");
            Trace.TraceInformation($"IPC-ADDRESS: {ipcAddress}");
            Trace.TraceInformation($"APP-NAME:    {appName}");
            Trace.TraceInformation("---------------------------------------");


            using (var host = new IpcHost())
            {
                var ipc = IpcProcessFactory.CreateProcess(host, difType, $"{ClientName}.{Process.GetCurrentProcess().Id.ToString()}");
                var flowInformation = new FlowInformation()
                {
                    SourceApplication = new ApplicationNamingInfo("TimeClient", "1", "TimeServiceProtocol", "1"),
                    DestinationApplication = new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "1"),
                    SourceAddress = ipc.LocalAddress,
                    DestinationAddress = Address.PipeAddressUri("localhost", ipcAddress)
                };

                var port = ipc.Connect(flowInformation);
                if (port != null)
                {
                    var cmdBytes = Encoding.ASCII.GetBytes("DateTime.Now\n");
                    port.Send(cmdBytes, 0, cmdBytes.Length);
                    var answerBuffer = port.Receive();
                    var answerString = Encoding.ASCII.GetString(answerBuffer, 0, answerBuffer.Length);
                    Console.WriteLine($"Current remote time is: {answerString}");
                    Trace.TraceInformation("Closing port...");
                    port.Shutdown(Timeout.InfiniteTimeSpan);               
                    Trace.WriteLine("Done, Bye.");
                }
                else
                {
                    Trace.TraceError($"Connection refused to {flowInformation.DestinationApplication}@{flowInformation.DestinationAddress}.");
                }
            }
        }
    }
}
