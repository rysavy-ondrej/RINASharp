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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Rina;
using System.Net.Rina.Shims;
using System.Text;
using System.Threading.Tasks;

namespace TimeServiceClient
{
    /// <summary>
    /// Inmplements a simple program that prints time information by querying TimeService.
    /// </summary>
    class ClientProgram
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;

            var ipcHost = new IpcHost();
            var shimIpcProcess = WcfServiceIpcProcess.Create("client" + Process.GetCurrentProcess().Id.ToString());
            var flowInformation = new FlowInformation()
            {
                SourceApplication = new ApplicationNamingInfo("TimeClient", "1", "TimeServiceProtocol", "V1"),
                DestinationApplication = new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "V1"),
                SourceAddress = shimIpcProcess.LocalAddress,
                DestinationAddress = Address.FromWinIpcPort("server")
            };
            
            var port = shimIpcProcess.AllocateFlow(flowInformation);
            if (port != null)
            {
                var cmdBytes = Encoding.ASCII.GetBytes("DateTime.Now\n");
                shimIpcProcess.Send(port, cmdBytes, 0, cmdBytes.Length);
                var answerBuffer = shimIpcProcess.Receive(port);
                var answerString = Encoding.ASCII.GetString(answerBuffer, 0, answerBuffer.Length);
                Console.WriteLine($"Current remote time is: {answerString}");
                Trace.WriteLine("Deallocating Flow...", "INFO");
                shimIpcProcess.DeallocateFlow(port);
                Trace.WriteLine("Disposing IpcProcess...", "INFO");
                shimIpcProcess.Dispose();
                Trace.WriteLine("Bye.");
            }
            else
            {
                Trace.WriteLine($"Cannot connect to {flowInformation.DestinationApplication}@{flowInformation.DestinationAddress}.", "ERROR");
            }
        }
    }
}
