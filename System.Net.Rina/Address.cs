//
//  Address.cs
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
	/// Specifies the addressing scheme that an instance of the Address class can use.
	/// </summary>
	public enum AddressFamily
	{
		Atm,
		Cluster,
		DatLink,
		Ecma,
		/// <summary>
		/// This is generic address, which means that 
		/// </summary>
		Generic,
		InterNetwork,
		InterNetworkV6,
		Osi,
		Mac,
		NetBios
	}
	/// <summary>
	/// Represents a generic address that can be used in RINA for addressing DIF nodes. 
	/// </summary>
	public class Address
	{
		AddressFamily _family;
		byte[] _buffer;


		public byte this[int offset] 
		{ 
			get { return this._buffer [offset]; } 
			set { this._buffer [offset] = value; } 
		}

		public Address (AddressFamily family, byte []rawAddress)
		{
			this._family = family;
			this._buffer = rawAddress;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="System.Net.Rina.Address"/> class using provided IPAddress.
		/// </summary>
		/// <param name="address">Address.</param>
		public Address (IPAddress address)
		{
			this._family = (AddressFamily)address.AddressFamily;
			this._buffer = address.GetAddressBytes ();
		}
		/// <summary>
		/// Gets the AddressFamily enumerated value of the current Address.
		/// </summary>
		/// <value>The family.</value>
		public AddressFamily Family { get { return this._family; } }

		/// <summary>
		/// This property gets the underlying buffer size of the SocketAddress in bytes.	
		/// </summary>
		/// <value>The size.</value>
		public int Size { get { return this._buffer.Length; } }
	}
}

