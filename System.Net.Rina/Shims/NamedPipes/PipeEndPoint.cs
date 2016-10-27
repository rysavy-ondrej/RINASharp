using System.Net.Sockets;

namespace System.Net.Rina.Shims.NamedPipes
{
    [Serializable]
    public class PipeEndPoint : EndPoint
    {
        private Address m_Address;
        private string m_Application;

        public override AddressFamily AddressFamily
        {
            get
            {
                return m_Address.Family;
            }
        }

        /// <summary>
        /// Creates a new instance of the PipeEndPoint class with the specified address and application.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="pipeName"></param>
        /// <param name="application"></param>
        public PipeEndPoint(string hostName, string pipeName, string application)
        {
            m_Application = application;
            m_Address = Address.PipeAddressUnc(hostName, pipeName);
        }

        /// <summary>
        /// Creates a new instance of the PipeEndPoint class with the specified address and application.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="application"></param>
        public PipeEndPoint(Address address, string application)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }
            m_Application = application;
            m_Address = address;
        }


    }; 
}