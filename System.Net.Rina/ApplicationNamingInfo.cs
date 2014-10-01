//
//  ApplicationNaming.cs
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
	/// Application naming info structure provides identification of 
    /// a unique application entity instance within DIF.
	/// </summary>
	/// <remarks>
	/// ProcessName corresponds to the application that uses RINA to communicate. For instance, this may be 
	/// "iexplorer.exe".  
	/// 
	/// ProcessInstance corresponds to the instance of the application process. For instance
	/// this may corresponds to PID of the application within OS.
	/// 
	/// EntityName is the corresponding entity within RINA DIF to the application process. For instance, 
	/// Internet Explorer uses http service to communicate, thus this may be httpsvc service.
	/// 
	/// EntityInstance is PID of httpsvc.  
	/// </remarks>
	public class ApplicationNamingInfo
	{
		/// <summary>
		/// Gets the name of the process.
		/// </summary>
		/// <value>The name of the process.</value>
		public String ProcessName { get; private set; }
		/// <summary>
		/// Gets the process instance.
		/// </summary>
		/// <value>The process instance.</value>
		public String ProcessInstance { get; private set; }
		/// <summary>
		/// Gets the name of the application entity which is used by application process.
		/// </summary>
		/// 
		/// <value>The name of the entity.</value>
		public String EntityName { get; private set; }
		/// <summary>
		/// Gets the entity instance as used by the application process instance.
		/// </summary>
		/// <value>The entity instance.</value>
		public String EntityInstance { get; private set; }


		public ApplicationNamingInfo(String apName, String apInstance, String aeName, String aeInstance)
		{
			this.ProcessName = apName;
			this.ProcessInstance = apInstance;
			this.EntityName = aeName;
			this.EntityInstance = aeInstance;
		}
	}
}

