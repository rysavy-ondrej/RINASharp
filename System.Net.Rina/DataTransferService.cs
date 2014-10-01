﻿//
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
using System;

namespace System.Net.Rina
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


		protected override void Run ()
		{
			while (true) {

			}
		}

		protected override void Finalize ()
		{
			throw new NotImplementedException ();
		}

		#endregion
	}


	public class DataTransferControlService : ApplicationEntity
	{
		public DataTransferControlService () : base("DataTransfer", "1", "DataTransferControlProtocol", "1")
		{
		}

		#region implemented abstract members of ApplicationEntity

		protected override bool Initialize ()
		{
			throw new NotImplementedException ();
		}

		protected override void Run ()
		{
			throw new NotImplementedException ();
		}

		protected override void Finalize ()
		{
			throw new NotImplementedException ();
		}

		#endregion
	}
}

