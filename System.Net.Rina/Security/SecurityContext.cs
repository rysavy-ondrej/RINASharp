using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net.Rina;
namespace System.Net.Rina.Security
{

    /// <summary>
    /// Security context is associated with a flow and contains setting of SDU protection mechanisms. 
    /// </summary>
    public class SecurityContext
    {
        /// <summary>
        /// Gets the flow associated with this security context.
        /// </summary>
        public FlowInstance FlowInstance { get; private set; }
        public SduCompressionMethod CompressionMethod { get; private set; }
        public SduEncryptionMethod EncryptionMethod { get; private set; }
        public SduIntegrityMethod IntegrityMethod { get; private set; }
        public SduLifelimitMethod LifelimitMethod { get; private set; }

        public SecurityContext Create(FlowInstance flowInstance, SduCompressionMethod compressionMethod, SduEncryptionMethod encryptionMethod, SduIntegrityMethod integrityMethod, SduLifelimitMethod lifelimitMethod)
        {
            var sc = new SecurityContext()
            {
                FlowInstance = flowInstance,
                CompressionMethod = compressionMethod,
                EncryptionMethod = encryptionMethod,
                IntegrityMethod = integrityMethod,
                LifelimitMethod = lifelimitMethod
            };
            return sc;
        }
    }
}
