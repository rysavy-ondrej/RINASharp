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
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace System.Net.Rina
{
    /// <summary>
    /// Represents a generic address that can be used in RINA for addressing DIF nodes. Each context should have defined 
    /// address scheme. Currently the address only supports families defined by <see cref="AddressFamily"/> enumerations.
    /// </summary>
    /// <remarks>
    /// Except <see cref="AddressFamily.InterNetwork"/>, <see cref="AddressFamily.InterNetworkV6"/> 
    /// address families the library uses following families for addressing RINA and Shim DIFs:
    /// <list type="table">
    /// <listheader>
    /// <term>term</term>
    /// <description>description</description>
    /// </listheader>
    /// <item>
    /// <term><see cref="AddressFamily.DataLink"/></term>
    /// <description>Direct data-link (MAC) interface address, stored directly as 6 bytes.</description>
    /// </item>
    /// <item>
    /// <term><see cref="AddressFamily.Unix"/></term>
    /// <description>This type is used in <see cref="Address"/> to store <see cref="Uri"/> object. Because 
    /// <see cref="Uri"/> can also represent UNC it can be used for pipe names too.</description>
    /// </item>
    /// </list>
    /// </remarks>     
    [Serializable]
    public class Address
	{
        /// <summary>
        /// Specifies <see cref="Address.Uri"/> to be alias for <see cref="AddressFamily.Unix"/>.
        /// </summary>
        public const AddressFamily Uri = AddressFamily.Unix;

        /// <summary>
        /// Address family.
        /// </summary>
		AddressFamily _family;
        /// <summary>
        /// Address content. 
        /// </summary>
		object _data;
        /// <summary>
        /// Buffer that caches a byte representation of <see cref="Address"/> object. 
        /// </summary>
        byte[] _buffer;
        /// <summary>
        /// Gets the byte representation of the <see cref="Address"/> address object.
        /// </summary>
        /// <returns></returns>
        byte[] getBuffer()
        {
            if (_buffer==null)
            {
                switch(this._family)
                {
                    case Address.Uri:
                        _buffer = Text.ASCIIEncoding.ASCII.GetBytes(((Uri)_data).ToString());
                        break;
                    case AddressFamily.InterNetwork:
                    case AddressFamily.InterNetworkV6:
                        _buffer = ((IPAddress)_data).GetAddressBytes();
                        break;
                    case AddressFamily.DataLink: // mac address
                        _buffer = (byte[])_data;
                        break;
                }
            }
            return _buffer;
        }


        public byte[] Raw { get { return this.getBuffer();  } }

        /// <summary>
        /// Gets a specified byte in <see cref="Address"/> represented as byte array.
        /// </summary>
        /// <param name="offset">An offset of accessed byte.</param>
        /// <returns>A value of the byte at the specified offset.</returns>
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
            this._family = AddressFamily.Unix;
            this._data = uri;
        }

        /// <summary>
        /// Creates a new <see cref="Address"/> of type <see cref="Address.Uri"/>
        /// for the given host and path. Address is stored as URI of the following form: "net.pipe://host/path".ef="Uri"/> object. 
        /// </summary>
        /// <seealso cref="https://msdn.microsoft.com/en-us/library/windows/desktop/aa365783(v=vs.85).aspx"/>
        /// <param name="host"> is either the name of a remote computer or a 'localhost', to specify the local computer. </param>
        /// <param name="path"> is Unix-like path specifying the pipe object. </param>
        /// <returns>An instance of <see cref="Address"/> of type <see cref="Address.Uri"/>
        /// for the given <paramref name="host"/> and <paramref name="path"/>.</returns>
        public static Address PipeAddressUri(string host, string path)
        {
            var uri = new Uri($"net.pipe://{host}/{path}");
            return new Address() { _family = Address.Uri, _data = uri };
        }

        /// <summary>
        /// Creates UNC representation of pipe name which has the form of "\\host\pipe\path".
        /// </summary>
        /// <param name="host">is either the name of a remote computer or a '.', to specify the local computer.</param>
        /// <param name="path">can include any character other than a backslash, including numbers and special characters.</param>
        /// <returns></returns>
        public static Address PipeAddressUnc(string host, string path)
        {
            var uri = new Uri($@"\\{host}\pipe\{path}");
            return new Address() { _family = Address.Uri, _data = uri };
        }

        /// <summary>
        /// Creates a generic address from the provided bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public Address(byte[] bytes)
        {
            this._family = AddressFamily.Unspecified;
            this._data = bytes;
        }

        /// <summary>
        /// Creates an address for provided MAC bytes.
        /// </summary>
        /// <param name="macBytes"></param>
        /// <returns></returns>
        public static Address DataLink(byte[] macBytes)
        {
            return new Address(macBytes) { _family = AddressFamily.DataLink };
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
                case AddressFamily.DataLink:
                    return String.Join("-", this.getBuffer().Select(x => x.ToString()));
                case Address.Uri:
                    return (((Uri)_data).ToString());
                case AddressFamily.InterNetwork:
                case AddressFamily.InterNetworkV6:
                    return ((IPAddress)_data).ToString();
                default:                    
                    return byteArrayToPrettyString(this.getBuffer());
            }
        }

        /// <summary>
        /// Gets the string representation of byte array. The C# syntax is used to represent the byte array.
        /// </summary>
        /// <param name="bytes">An input byte array to represents as string.</param>
        /// <returns>A string representation of byte array.</returns>
        static string byteArrayToPrettyString(byte[] bytes)
        {
            var sb = new Text.StringBuilder("new byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            return sb.ToString();
        }


        /// <summary>
        /// Serializes the current object to the provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> object to which the object will be serialized. </param>
        public void Serialize(Stream stream)
        {
            using (var wr = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                wr.Write((uint)Family);
                switch (Family)
                {
                    case AddressFamily.DataLink:
                    case AddressFamily.InterNetwork:
                    case AddressFamily.InterNetworkV6:
                        wr.Write(this.getBuffer());
                        break;
                    case Address.Uri:
                        wr.Write((_data as Uri)?.ToString());
                        break;
                    default:
                        wr.Write(this.getBuffer());
                        break;
                }
            }
        }

        public static Address Deserialize(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var af = (AddressFamily)br.ReadUInt32();
                switch (af)
                {
                    case AddressFamily.DataLink:
                        var dl = br.ReadBytes(6);
                        return Address.DataLink(dl);
                    case AddressFamily.InterNetwork:
                        var ipv4 = new IPAddress(br.ReadBytes(4));
                        return new Address(ipv4);
                    case AddressFamily.InterNetworkV6:
                        var ipv6 = new IPAddress(br.ReadBytes(16));
                        return new Address(ipv6);
                    case Address.Uri:
                        var uri = br.ReadString();
                        return new Address(new Uri(uri));
                    default:
                        throw new NotSupportedException();
                }

            }
        }

        public override int GetHashCode()
        {
            return Family.GetHashCode() ^ (int)(Value?.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            var other = obj as Address;
            if (other == null) return false;

            var iseq = Family == other.Family && Object.Equals(Value, other.Value);
            return iseq;
        }

    }

    /// <summary>
    /// An extension of <see cref="Uri"/> class with various RINA-related operations. 
    /// </summary>
    public static class RinaUri
    {
        public static string GetDifName(this Uri uri)
        {
            return uri.Host;
        }
        public static string GetIpcAddress(this Uri uri)
        {
            var path = uri.AbsolutePath;
            var components = path.TrimStart(Path.PathSeparator, Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Split(Path.PathSeparator, Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return components[0];
        }

        public static string GetAppName(this Uri uri)
        {
            // TODO: Hard-coded for the moment, replace with proper URI handling!!!
            var path = uri.AbsolutePath;
            var components = path.TrimStart(Path.PathSeparator, Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Split(Path.PathSeparator, Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return components[2];
        }

        /// <summary>
        /// Gets UNC representation for the <see cref="Uri"/> of the form: "net.pipe://host/path". 
        /// </summary>
        /// <param name="uri">An <see cref="Uri"/> object that specifies pipe name.It should have the form of "net.pipe://host/path".</param>
        /// <returns>An <see cref="Uri"/> of the form "\\host\pipe\path" or null if the <paramref name="uri"/> is of incorrect format.</returns>
        public static Uri AsPipeNameUnc(this Uri uri)
        {
            if (String.Equals(uri.Scheme, "net.pipe"))
            {
                var host = uri.Host;
                var path = uri.PathAndQuery.Replace('/', '\\');
                return new Uri($@"\\{host}\{path}");
            }
            else return null;
        }
    }
}

