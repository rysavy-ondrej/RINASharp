﻿//
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
namespace System.Net.Rina
{
	/// <summary>
	/// Specifies the addressing scheme that an instance of the Address class can use.
	/// </summary>
	public enum AddressFamily
	{
        /// <summary>
        /// Address for Microsoft cluster products.
        /// </summary>
        Cluster = System.Net.Sockets.AddressFamily.Cluster,
		/// <summary>
		/// This represents IPv4 address.
		/// </summary>
        InterNetwork = System.Net.Sockets.AddressFamily.InterNetwork,
        /// <summary>
        /// This represents IPv6 address.
        /// </summary>
        InterNetworkV6 = System.Net.Sockets.AddressFamily.InterNetworkV6,
        /// <summary>
        /// This represents NetBios address.
        /// </summary>
        NetBios = System.Net.Sockets.AddressFamily.NetBios,
        /// <summary>
        /// Address for OSI protocols.
        /// </summary>
        Osi = System.Net.Sockets.AddressFamily.Osi,
        /// <summary>
        /// This is Ethernet Mac address.
        /// </summary>	
        Mac = 32, 
        /// <summary>
        /// This represent URI address.
        /// </summary>
        Uri = 33,
        /// <summary>
        /// This address represents Ipc port name.
        /// </summary>
        WinIpc = 34,
		/// <summary>
		/// This is generic address, which means that it is provided as a sequence of bytes and interpreted by a user.
		/// </summary>
        Generic = System.Net.Sockets.AddressFamily.Unspecified
	}
	/// <summary>
	/// Represents a generic address that can be used in RINA for addressing DIF nodes. Each context should have defined 
    /// address scheme. Currently the address only supports families defined by <see cref="AddressFamily"/> enumerations.
	/// </summary>
	public class Address
	{
        /// <summary>
        /// Address family.
        /// </summary>
		AddressFamily _family;
        /// <summary>
        /// Address content. 
        /// </summary>
		object _data;

        byte[] _buffer;
        byte[] getBuffer()
        {
            if (_buffer==null)
            {
                switch(this._family)
                {
                    case AddressFamily.WinIpc:
                        _buffer = Text.ASCIIEncoding.ASCII.GetBytes(((string)_data).ToString());
                        break;
                    case AddressFamily.Uri:
                        _buffer = Text.ASCIIEncoding.ASCII.GetBytes(((Uri)_data).ToString());
                        break;
                    case AddressFamily.InterNetwork:
                    case AddressFamily.InterNetworkV6:
                        _buffer = ((IPAddress)_data).GetAddressBytes();
                        break;
                    case AddressFamily.Generic:
                    case AddressFamily.Mac:
                        _buffer = (byte[])_data;
                        break;
                }
            }
            return _buffer;
        }

		public byte this[int offset] 
		{
            get { return getBuffer()[offset]; }
		}

        /// <summary>
        /// Creates a new <see cref="System.Net.Rina.Address"/> object for the specified AddressFamily and address value represented as byte array.
        /// </summary>
        /// <param name="family">An AddressFamily representing the kind of address.</param>
        /// <param name="rawAddress">Byte array representing raw address of the given AddressFamily.</param>
		public Address (AddressFamily family, byte []rawAddress)
		{
			this._family = family;
            this._data = rawAddress;
		}

        private Address ()
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="System.Net.Rina.Address"/> class using provided IPAddress.
        /// </summary>
        /// <param name="address">An IPAddress value used to create <see cref="System.Net.Rina.Address"/>.</param>
        public Address (IPAddress address)
		{
			this._family = (AddressFamily)address.AddressFamily;
            this._data = address;
		}


        /// <summary>
        /// Creates a new instance of <see cref="System.Net.Rina.Address"/> class using <see cref="System.Uri"/> as the underlaying address. 
        /// </summary>
        /// <param name="uri">An instance of <see cref="System.Uri"/> representing the address. </param>
        public Address (Uri uri)
        {
            this._family = AddressFamily.Uri;
            this._data = uri;
        }

        /// <summary>
        /// Creates  <see cref="System.Net.Rina.Address"/> from <see cref="portName"/> string. Address string then looks like 'net.pipe://localhost/portName'.
        /// </summary>
        /// <param name="portName">A name of local IPC port used for creating IPC connection address.</param>
        /// <returns></returns>
        public static Address FromWinIpcPort(string portName)
        {
            var ipcName = String.Format("net.pipe://localhost/{0}", portName);
            var adr = new Address() { _family = AddressFamily.WinIpc, _data = ipcName };
            return adr;
        }
        /// <summary>
        /// Creates Address from WinIpc string. 
        /// </summary>
        /// <param name="ipcName">A WinIpc string used for creating IPC connection address.</param>
        /// <returns></returns>
        public static Address FromWinIpcString(string ipcName)
        {
            var adr = new Address() { _family = AddressFamily.WinIpc, _data = ipcName };
            return adr;
        }

        /// <summary>
        /// Creates a generic address from the provided bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public Address(byte[] bytes)
        {
            this._family = AddressFamily.Generic;
            this._data = bytes;
        }

        /// <summary>
        /// Creates an address for provided MAC bytes.
        /// </summary>
        /// <param name="macBytes"></param>
        /// <returns></returns>
        public static Address FromMac(byte[] macBytes)
        {
            return new Address(macBytes) { _family = AddressFamily.Mac };
        }

		/// <summary>
		/// Gets the <see cref="AddressFamily"/>  AddressFamily enumerated value of the current Address.
		/// </summary>
		/// <value>The family.</value>
		public AddressFamily Family { get { return this._family; } }

		/// <summary>
		/// This property gets the underlying buffer size of the SocketAddress in bytes.	
		/// </summary>
		/// <value>The size.</value>
		public int Size { get { return this.getBuffer().Length; } }

        /// <summary>
        /// Gets a value of the current <c>Address</c> object.
        /// </summary>
        public object Value {  get { return this._data; } }


        public override string ToString()
        {
            switch (this._family)
            {
                case AddressFamily.WinIpc:
                    return (string)_data;
                case AddressFamily.Uri:
                    return (((Uri)_data).ToString());
                case AddressFamily.InterNetwork:
                case AddressFamily.InterNetworkV6:
                    return ((IPAddress)_data).ToString();
                default:                    
                    return ByteArrayToString(this.getBuffer());
            }
        }

        /// <summary>
        /// Gets the string representation of byte array. The C# syntax is used to represent the byte array.
        /// </summary>
        /// <param name="bytes">An input byte array to represents as string.</param>
        /// <returns>A string representation of byte array.</returns>
        public static string ByteArrayToString(byte[] bytes)
        {
            var sb = new Text.StringBuilder("new byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}

