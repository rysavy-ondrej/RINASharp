using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Rina;
namespace System.Net.Rina.Security
{

    /// <summary>
    /// This class stores informaiton about SDU protection checking result.
    /// </summary>
    class SduProtectionResult
    {
           
    }

    class SduProtection
    {

        /// <summary>
        /// Allocates a byte array that can accomodate all data of length specified in <paramref name="dataLength"/>and
        /// also metadata associated with SDU protection for specified <paramref name="flow"/>.
        /// </summary>
        /// <param name="flow"></param>
        /// <param name="dataLen"></param>
        /// <returns></returns>
        public byte [] AllocateSduBuffer(FlowInstance flow, int dataLength)
        {
            return null;
        }

        /// <summary>
        /// Applies SDU protection policy as set for <paramref name="flow"/>. It assumes that 
        /// <paramref name="buffer"/> contains enough space for adding protective data. 
        /// </summary>
        /// <param name="flow"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public bool ApplyProtection(FlowInstance flow, byte[] buffer)
        {
            
            return false;
        }

        public SduProtectionResult CheckProtection(FlowInstance flow, byte[] buffer)
        {

            return null;
        }


    }
}
