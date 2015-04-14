using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Rina;
namespace System.Net.Rina.Naming
{
    public class StaticNameService : NameService
    {        
        Dictionary<String, IpcLocationVector> _applicationAddressMap = new Dictionary<string, IpcLocationVector>();

        public void AddApplicationAddress(string applicationProcessName, string applicationEntityName, IpcLocationVector location)
        {
            lock (_applicationAddressMap)
            {
                _applicationAddressMap[applicationProcessName + "$" + applicationEntityName] = location;
            }
        }

        public override IpcLocationVector[] GetApplicationAddresses(string applicationProcessName, string applicationEntityName)
        {
            lock (_applicationAddressMap)
            {
                IpcLocationVector value = null;
                if (_applicationAddressMap.TryGetValue(applicationProcessName + "$" + applicationEntityName, out value))
                {
                    return new IpcLocationVector[] { value };
                }
                else
                {
                    return null;
                }
            }
        }

        public override Task<IpcLocationVector[]> GetApplicationAddressesAsync(string applicationProcessName, string applicationEntityName)
        {
            throw new NotImplementedException();
        }

        protected override bool Initialize()
        {
            return true;
        }

        protected override void Run()
        {
            throw new NotImplementedException();
        }
    }
}
