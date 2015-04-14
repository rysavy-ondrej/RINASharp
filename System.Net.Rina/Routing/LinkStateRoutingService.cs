//
//  RoutingService.cs
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

namespace System.Net.Rina.Routing
{
	/// <summary>
	/// This is base class for all routing services. 
	/// </summary>
	public class LinkStateRoutingService : RoutingService
	{
		public LinkStateRoutingService () : base("LinkStateRoutingProtocol", "1")
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

		#endregion
	}
}

