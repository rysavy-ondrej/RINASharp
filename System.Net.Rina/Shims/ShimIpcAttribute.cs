using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ShimIpcAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of Shim DIF for the defined IpcContext.
        /// </summary>
        public String Name { get; private set; }
        public ShimIpcAttribute(string name)
        {
            this.Name = name;
        }
    }
}
