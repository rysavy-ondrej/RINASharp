//
//  Flow.cs
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
	/// Flow information object describes parameters of the flow to be created 
	/// between two IPCs. It represents flow requirements sent by 
    /// the client process to supporting process in order to allocate a new flow.  
	/// </summary>
	public class FlowInformation
	{
		public ApplicationNamingInfo SourceApplication; 
		public ApplicationNamingInfo DestinationApplication; 
		public Address SourceAddress; 
		public Address DestinationAddress; 
		public QosParameters QosParameters; 
		public IpcPolicies Policies; 
		public UInt16 CreateFlowRetriesLimit; 
		public UInt16 HopCountLimit;
	}
}

