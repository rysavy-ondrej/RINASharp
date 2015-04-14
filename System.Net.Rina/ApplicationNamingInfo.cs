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
using System.Net.Rina.Shims;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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
	/// ProcessInstance corresponds to the instance of the application process. Some applications 
    /// enable to run multiple instances and in this case these instances may be distinguished.
	/// 
	/// EntityName is the corresponding entity within RINA DIF to the application process. For instance, 
	/// Internet Explorer uses http service to communicate, thus this may be httpsvc service.
	///   
	/// </remarks>
	public class ApplicationNamingInfo : ISerializable 
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

        public override int GetHashCode()
        {
            return (this.ProcessName ?? String.Empty).GetHashCode() ^
                   (this.ProcessInstance ?? String.Empty).GetHashCode() ^
                   (this.EntityName ?? String.Empty).GetHashCode() ^
                   (this.EntityInstance ?? String.Empty).GetHashCode();
        }
        public override bool Equals(object obj)
        {
            var that = (ApplicationNamingInfo)obj;
            return (that != null) ?
                    String.Equals(this.ProcessName, that.ProcessName, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(this.ProcessInstance, that.ProcessInstance, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(this.EntityName, that.EntityName, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(this.EntityInstance, that.EntityInstance, StringComparison.InvariantCultureIgnoreCase)
                : false;
        }

        public ApplicationNamingInfo(String apName, String apInstance, String aeName, String aeInstance)
		{
			this.ProcessName = apName;
			this.ProcessInstance = apInstance;
			this.EntityName = aeName;
			this.EntityInstance = aeInstance;
		}


        public override string ToString()
        {
            return this.ProcessName + "." + this.ProcessInstance + "+" + this.EntityName + "." + this.EntityInstance;
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Tests whether the current instance matches <c>ApplicationNamingInfo</c> object given as parameter <c>that</c>.
        /// </summary>
        /// <remarks>
        /// To match this instance, that object must matches in each of its property. This object can use null value for its properties
        /// that represent a wildcard for matching procedure. 
        /// </remarks>
        /// <param name="that">An instance of ApplicationNamingInfo.</param>
        /// <returns>True if this obejct matches that object.</returns>
        internal bool Matches(ApplicationNamingInfo that)
        {
            return (String.Equals(this.ProcessName, that.ProcessName, StringComparison.InvariantCultureIgnoreCase) || String.IsNullOrWhiteSpace(this.ProcessName)) &&
                   (String.Equals(this.ProcessInstance, that.ProcessInstance, StringComparison.InvariantCultureIgnoreCase) || String.IsNullOrWhiteSpace(this.ProcessInstance)) &&
                   (String.Equals(this.EntityName, that.EntityName, StringComparison.InvariantCultureIgnoreCase) || String.IsNullOrWhiteSpace(this.EntityName)) &&
                   (String.Equals(this.EntityInstance, that.EntityInstance, StringComparison.InvariantCultureIgnoreCase) || String.IsNullOrWhiteSpace(this.EntityInstance));
        }
    }
}

