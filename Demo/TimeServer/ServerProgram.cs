//
//  ServerProgram.cs
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
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Rina;
using System.Net.Rina.Shims;
using System.Threading;
using System.Threading.Tasks;
namespace TimeService
{
    /// <summary>
    /// This is a simple program that registers itself as a time server in the Time Services DIF. 
    /// </summary>
    class ServerProgram
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;
            using (var host = new IpcHost())
            {
                var shimIpcProcess = WcfServiceIpcProcess.Create("server");
                shimIpcProcess.RegisterApplication(new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "V1"), applicationRequestHandler);

                var evh = new EventWaitHandle(false, EventResetMode.AutoReset);
                Console.CancelKeyPress += new ConsoleCancelEventHandler((x, a) => evh.Set());
                evh.WaitOne();
            }                      
        }

        /// <summary>
        /// This method decides whether to accept or reject the request. 
        /// </summary>
        private static ConnectionRequestResult applicationRequestHandler(IRinaIpc context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler)
        {
            Console.WriteLine($"FlowInfo:{flowInformation.SourceApplication}@{flowInformation.SourceAddress} <-> {flowInformation.DestinationApplication}@{flowInformation.DestinationAddress}");
            acceptFlowHandler = ServerProgram.acceptFlowHandler;
            return ConnectionRequestResult.Accept;
        }
        /// <summary>
        /// This method is called for creating a new instance for the server and executing its worker thread.
        /// </summary>
        private static Task acceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port)
        {
           return Task.Run(() =>
           {
               port.Blocking = true;
               using (var stream = new IpcChannelStream(port))
               using (var tr = new StreamReader(stream))
               using (var tw = new StreamWriter(stream))
               {
                    // A line is defined as a sequence of characters followed by a line feed ("\n"), a carriage return ("\r") or a carriage return immediately followed by a line feed("\r\n").
                    // The string that is returned does not contain the terminating carriage return or line feed. The returned value is a null reference if the end of the input stream is reached.
                   var line = tr.ReadLine();
                   switch ((line ?? String.Empty).Trim())
                   {
                       case "DateTime.Now": tw.WriteLine($"OK: {DateTime.Now}"); break;
                       default: tw.WriteLine("ERROR: Invalid request!"); break;
                   }
               }
           });
        }

    }
}
