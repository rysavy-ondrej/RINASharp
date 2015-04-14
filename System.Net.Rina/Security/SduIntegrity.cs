using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net.Rina;
namespace System.Net.Rina.Security
{

    public enum IntegrityAlgorithm { Null, CRC, MD5, RIPEMD160, SHA1, SHA256, SHA384, SHA512 }
    public class SduIntegrityPolicy : IpcPolicy
    {
        public PaddingMode PaddingMode { get { return (PaddingMode)this.parameters["PaddingMode"]; } }

        public string Algorithm { get { return (string)this.parameters["Algorithm"]; } }

        public int KeySize { get { return (int)this.parameters["KeySize"]; } }
        public SduIntegrityPolicy(string algorithm, int keySize, PaddingMode paddingMode)
        {
            this.parameters["Algorithm"] = algorithm;
            this.parameters["PaddingMode"] = paddingMode;
            this.parameters["KeySize"] = keySize;
        }

        /// <summary>
        /// The size, in bits, of the computed hash code.
        /// </summary>
        public virtual int HashSize { get { return 64; } }

        public override IpcPolicyType PolicyType
        {
            get { return IpcPolicyType.SduIntegrityPolicy; }
        }
    }

    public abstract class SduIntegrityMethod
    {
        protected byte[] _key;
        public abstract byte[] ComputeChecksum(byte[] input);
        public abstract bool CheckChecksum(byte[] input, byte[] checksum);

        public abstract IpcPolicy Policy { get; }

        /// <summary>
        /// Gets or sets the key of HMAC method.
        /// </summary>
        public byte[] Key { get; set; }
    }


    class SduNullIntegrityMethod : SduIntegrityMethod
    {
        SduIntegrityPolicy _policy = new SduIntegrityPolicy(IntegrityAlgorithm.Null.ToString(), 0, PaddingMode.None);
        public override byte[] ComputeChecksum(byte[] input)
        {
            return null;
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            return true;
        }

        public override IpcPolicy Policy
        {
            get { return this._policy; }
        }
    }

    class SduCrcIntegrityMethod : SduIntegrityMethod
    {

        SduIntegrityPolicy _policy = new SduIntegrityPolicy(IntegrityAlgorithm.CRC.ToString(), 32, PaddingMode.None);
        public override byte[] ComputeChecksum(byte[] input)
        {
            return BitConverter.GetBytes(DamienG.Security.Cryptography.Crc32.Compute(input));
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            var computed = DamienG.Security.Cryptography.Crc32.Compute(input);
            var given = BitConverter.ToUInt32(checksum, 0);
            return computed == given;
        }

        public override IpcPolicy Policy
        {
            get { return this._policy; }
        }
    }
    class SduMd5IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }

    class SduRipeMd160IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }

    class SduRipeSha1IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }
    class SduRipeSha256IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }
    class SduRipeSha384IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }

    class SduRipeSha512IntegrityMethod : SduIntegrityMethod
    {

        public override byte[] ComputeChecksum(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override bool CheckChecksum(byte[] input, byte[] checksum)
        {
            throw new NotImplementedException();
        }

        public override IpcPolicy Policy
        {
            get { throw new NotImplementedException(); }
        }
    }
}
