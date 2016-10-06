//
//  NameService.cs
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
using System.Threading.Tasks;
using System.Net.Rina;
namespace System.Net.Rina.Naming
{

	/// <summary>
	/// This is an abstract class of nameing services that is supposed to maintain names within a single DIF. 
    /// The realization may depend on the architecture of name system. It may be as simple as a static 
    /// database replicated at all nodes as well as represented by the complex distributed naming system. 
	/// </summary>
	public abstract class NameService : ApplicationEntity
	{
		protected NameService () : base("ManagementService","1","NameServiceProtocol","1") {  }

        /// <summary>
        /// Gets <see cref="IpcLocationVector"/> array for the specified Application Process Name and Application Entity Name.
        /// </summary>
        /// <param name="applicationProcessName">A string value representing Application Process Name.</param>
        /// <param name="applicationEntityName">A string value representing Application Entity Name.</param>
        /// <returns>An array of <see cref="IpcLocationVector"/> object containting the address of APN/AEN or null if no record for APN/AEN was found.</returns>
		public abstract IpcLocationVector[] GetApplicationAddresses(string applicationProcessName, string applicationEntityName);

        /// <summary>
        /// Gets <see cref="IpcLocationVector"/> for the specified Application Process Name and Application Entity Name.
        /// </summary>
        /// <param name="applicationProcessName">A string value representing Application Process Name.</param>
        /// <param name="applicationEntityName">A string value representing Application Entity Name.</param>
        /// <returns><see cref="IpcLocationVector"/> containting the address of APN/AEN or null if APN/AEN cannot be found.</returns>
        public abstract Task<IpcLocationVector[]> GetApplicationAddressesAsync(string applicationProcessName, string applicationEntityName);
	}
}

