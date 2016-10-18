using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina.Helpers
{
    /// <summary>
    /// This class randomly generates new fresh value.
    /// </summary>
    public class UniqueRandomUInt64 
    {
        private readonly object _syncRoot = new Object();
        private readonly  Random random = new Random();
        private readonly  HashSet<ulong> occupied = new HashSet<ulong>();

        public ulong Next() 
        {
            lock (_syncRoot)
            {
                UInt64 value;          
                do
                {
                    var buffer = new byte[8];
                    random.NextBytes(buffer);
                    value = BitConverter.ToUInt64(buffer, 0);
                }
                while (occupied.Contains(value));
                occupied.Add(value);
                return value;
            }            
        }

        public void Release(ulong value)
        {
            lock (_syncRoot)
            {
                occupied.Remove(value);
            }
        }
    }

    /// <summary>
    /// This class randomly generates new fresh value.
    /// </summary>
    public class UniqueRandomUInt32
    {
        private readonly object _syncRoot = new Object();
        private readonly Random random = new Random();
        private readonly HashSet<uint> occupied = new HashSet<uint>();

        public uint Next()
        {
            lock (_syncRoot)
            {
                UInt32 value;
                do
                {
                    var buffer = new byte[4];
                    random.NextBytes(buffer);
                    value = BitConverter.ToUInt32(buffer, 0);
                }
                while (occupied.Contains(value));
                occupied.Add(value);
                return value;
            }
        }

        public void Release(uint value)
        {
            lock (_syncRoot)
            {
                occupied.Remove(value);
            }
        }
    }
}
