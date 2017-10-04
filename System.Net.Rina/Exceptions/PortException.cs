using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Rina
{

    [Serializable]
    public class IpcException : Exception
    {

        [NonSerialized]
        private EndPoint m_EndPoint;
        private int m_errorCode;

        /// <summary>
        /// Creates a new instance of the <see cref="System.Net.Rina.IpcException"> class with the default error code. 
        /// </summary>
        public IpcException() 
        {
        }

        internal IpcException(EndPoint endPoint) 
        {
            m_EndPoint = endPoint;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="System.Net.Rina.IpcException"> class with the default error code. 
        /// </summary>
        public IpcException(int errorCode) 
        {
            m_errorCode = errorCode;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="System.Net.Rina.IpcException"> class with the default error code. 
        /// </summary>
        internal IpcException(int errorCode, EndPoint endPoint) 
        {
            m_EndPoint = endPoint;
            m_errorCode = errorCode;
        }

        /// <devdoc> 
                ///    <para>
                ///       Creates a new instance of the <see cref="System.Net.Ports.PortException"> class with the specified error code as PortError. 
                ///    </see></para> 
                /// </devdoc>
        internal IpcException(IpcError portError) 
        {
            m_errorCode = (int)portError;
        }


        protected IpcException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        : base(serializationInfo, streamingContext)
        {
        }

        /// <devdoc> 
                ///    <para>[To be supplied.]</para>
                /// </devdoc>
        public int ErrorCode
        {
            // 
            // the base class returns the HResult with this property
            // we need the Win32 Error Code, hence the override. 
            // 
            get
            {
                return m_errorCode;
            }
        }

        public override string Message
        {
            get
            {
                // If not null add EndPoint.ToString() to end of base Message 
                if (m_EndPoint == null)
                {
                    return base.Message;
                }
                else
                {
                    return base.Message + " " + m_EndPoint.ToString();
                }
            }
        }


        public IpcError PortErrorCode
        {
            //
            // the base class returns the HResult with this property 
            // we need the Win32 Error Code, hence the override.
            //
            get
            {
                return (IpcError)m_errorCode;
            }
        }


    }; // class PortException 


} 