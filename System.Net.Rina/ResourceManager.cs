//
//  ResourceBase.cs
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
	/// Used to enumerate resource information register.
	/// </summary>
	public sealed class ResourceInformationKey
	{
		public String Name { get; private set; }
		public int ValueCount { get; private set; }
		public int SubKeyCount { get; private set; }
	}

	public sealed class ResourceClass
	{
		public String Name { get; private set; }


		private ResourceClass(string name)
		{
			this.Name = name;
		}

		/// <summary>
		/// Represents classes of application names. When application register itself 
		/// in DIF then the name is added to this class.
		/// </summary>
		public static readonly ResourceClass ApplicationNames = new ResourceClass("ApplicationNames");
		/// <summary>
		/// This class stores information related to addresssing. It contains addresses assigned to the current
		/// DIF as well as addresses of bootstrapping node, ... 
		/// </summary>
		public static readonly ResourceClass Addresses = new ResourceClass("Addresses");

	}

	/// <summary>
	/// This is resource database. All DIF information is maintained here in a uniform way. It is very like a windows registry structure. 
	/// </summary>
	public class ResourceInformationManager
	{
		NameServiceAE _nameService;
		public ResourceInformationManager(NameServiceAE nameService)
		{
			this._nameService = nameService;
		}
			

		/// <summary>
		/// Gets the value from Resource Registry.
		/// </summary>
		/// <returns>The value.</returns>
		/// <param name="keyName">The full registry path of the key, beginning with a valid registry root, such as "UNDERLYAING_DIFS".</param>
		/// <param name="valueName">The name of the name/value pair.</param>
		/// <param name="defaultValue">The value to return if valueName does not exist.</param>
		public Object GetValue(ResourceClass resourceClass, string keyName, string valueName, Object defaultValue)
		{
			// If name resolution is required, call NameService.
			if (resourceClass = ResourceClass.ApplicationNames) {
				// this is to be resolved by NS
				var result = _nameService.GetApplicationAddresses (valueName);
				if (result != null && result.Length > 0)
					return result [0];
				else
					return defaultValue; 
			}

			return defaultValue;
		}

		/// <summary>
		/// Sets the specified name/value pair on the specified registry key. If the specified key does not exist, it is created.
		/// </summary>
		/// <param name="keyName">The full registry path of the key, beginning with a valid registry root.</param>
		/// <param name="valueName">The name of the name/value pair.</param>
		/// <param name="value">The value to be stored.</param>
		public void SetValue(ResourceClass resourceClass, string keyName,string valueName,Object value)
		{
		}
			
		public void DeleteValue(String keyName, string valueName)
		{
		}
	}
}

