using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Systen.Net.Rina.Internals;

namespace System.Net.Rina.DataUnits
{
    internal class SduInternal
    {
                
        internal SduInternal(Port port, byte[] buffer, int offset, int size)
        {
            Port = port;
            UserData = new ByteArraySegment(buffer, offset, size);            
        }        
        internal Port Port { get; private set; }
        internal ByteArraySegment UserData { get; private set; }
    }
}
