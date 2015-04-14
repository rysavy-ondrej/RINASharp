//
//  DataTransferAE.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE
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
namespace System.Net.Rina.Transport
{
    /// <summary>
    /// This AE should implement sending and receiving data within this DIF. It serves all FlowInstance
    /// checking their buffers for available data. 
    /// </summary>
    public class DataTransferService : ApplicationEntity
	{
		public FlowManager FlowManager { get; private set; }
		public DataTransferService (FlowManager flowManager) : base("DataTransfer", "1", "DataTransferProtocol", "1")
		{
			this.FlowManager = flowManager;
		}

		#region implemented abstract members of ApplicationEntity

		protected override bool Initialize ()
		{
			throw new NotImplementedException ();
		}

        /// <summary>
        /// Processes all flows in the current IPC object. Processing means to check if there are data to send and also try to read data from underlaying DIFs.
        /// </summary>
		protected override void Run ()
		{
			while (true) {

			}
		}

		#endregion
	}
}

