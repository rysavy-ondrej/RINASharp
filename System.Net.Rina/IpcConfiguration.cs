 //
//  IpcConfiguration.cs
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
    /// Stores Ipc related configurations. 
    /// </summary>
	public struct IpcConfiguration
	{
        /// <summary>
        /// Represents a Dif address of the comfigured Ipcp.
        /// </summary>
        public string DifName { get; set; }
        /// <summary>
        /// Represents a local address of the configured Ipcp.
        /// </summary>
        public string HostName { get; set; }    

        public Uri DifUriAddress {  get { return new Uri("rina://" + this.DifName); } }

        public Uri HostUriAddress { get { return new Uri("/" + this.HostName, UriKind.Relative); } }
        /// <summary>
        /// Gets fully qualified name of the configured Ipcp.
        /// </summary>
        public Uri FullUriAddress {  get { return new Uri("rina://" + this.DifName + "/" + this.HostName); } }
	}
}

