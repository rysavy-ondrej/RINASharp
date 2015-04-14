//
//  IpcPolicies.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE Consortium (http://ict-pristine.eu)
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
namespace System.Net.Rina
{

    public enum IpcPolicyType
    {
        SduCompressionPolicy,
        SduEncryptionPolicy, 
        SduIntegrityPolicy, 
        SduLifelimitPolicy
    }

    public abstract class IpcPolicy : IReadOnlyDictionary<string,object>
    {
        protected Dictionary<string, object> parameters = new Dictionary<string, object>();
        public abstract IpcPolicyType PolicyType { get; }

        public bool ContainsKey(string key)
        {
            return this.parameters.ContainsKey(key);
        }

        public IEnumerable<string> Keys
        {
            get { return this.parameters.Keys; }
        }

        public bool TryGetValue(string key, out object value)
        {
            return this.parameters.TryGetValue(key, out value);
        }

        public IEnumerable<object> Values
        {
            get { return this.parameters.Values; }
        }

        public object this[string key]
        {
            get { return this.parameters[key]; }
        }

        public int Count
        {
            get { return this.parameters.Count; }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return this.parameters.GetEnumerator();
        }

        Collections.IEnumerator Collections.IEnumerable.GetEnumerator()
        {
            return this.parameters.GetEnumerator();
        }
    }


    /// <summary>
    /// This class represents a collection of policies for Ipc mechamisms.
    /// </summary>
	public class IpcPolicies
	{
		public IpcPolicies ()
		{
		}

	}
}

