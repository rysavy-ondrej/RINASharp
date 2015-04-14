using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Rina;
namespace System.Net.Rina.Security
{

    public enum LifelimitMethod { Null, Hopcount, Realtime }
    public class SduLifelimitPolicy : IpcPolicy
    {
        public LifelimitMethod Method { get { return (LifelimitMethod)this.parameters["Method"]; } }

        public SduLifelimitPolicy(string method)
        {
            this.parameters["Method"] = method;
        }

        public override IpcPolicyType PolicyType
        {
            get { return IpcPolicyType.SduLifelimitPolicy; }
        }
    }

    
    public abstract class SduLifelimitMethod
    {
        public abstract byte[] Apply(byte[] input);
        public abstract bool Expired(byte[] input);

        public abstract IpcPolicy Policy { get; }
    }

    class SduNullLifelimitMethod : SduLifelimitMethod
    {
        SduLifelimitPolicy _policy = new SduLifelimitPolicy(LifelimitMethod.Null.ToString());
        public override byte[] Apply(byte[] input)
        {
            return null;
        }

        public override bool Expired(byte[] input)
        {
            return false;
        }

        public override IpcPolicy Policy
        {
            get { return this._policy; }
        }
    }

    class SduHopCountLifelimitMethod : SduLifelimitMethod
    {
        SduLifelimitPolicy _policy = new SduLifelimitPolicy(LifelimitMethod.Hopcount.ToString());
        public override byte[] Apply(byte[] input)
        {
            switch(input.Length)
            {
                case 1:
                    return new byte[] { (byte)(input[0] - 1) };
                case 2:
                    return BitConverter.GetBytes((BitConverter.ToUInt16(input,0) - 1));
                case 4:
                    return BitConverter.GetBytes((BitConverter.ToUInt32(input, 0) - 1));
                case 8:
                    return BitConverter.GetBytes((BitConverter.ToUInt64(input, 0) - 1));                
                default:
                    throw new ArgumentException("Invalid size of input.");
            }
        }

        public override bool Expired(byte[] input)
        {
            switch (input.Length)
            {
                case 1:
                    return (input[0] - 1) == 0;
                case 2:
                    return ((BitConverter.ToUInt16(input, 0) - 1)) == 0;
                case 4:
                    return ((BitConverter.ToUInt32(input, 0) - 1)) == 0;
                case 8:
                    return ((BitConverter.ToUInt64(input, 0) - 1)) == 0;
                default:
                    throw new ArgumentException("Invalid size of input.");
            }
        }

        public override IpcPolicy Policy
        {
            get { return this._policy; }
        }
    }
}
