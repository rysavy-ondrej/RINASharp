using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net.Rina;
namespace System.Net.Rina.Security
{
    
    public enum EncryptionAlgorithm { Null, AES, DES, RC2, Rijndael, TripleDES }
    
    public class SduEncryptionPolicy : IpcPolicy
    {
        public CipherMode CipherMode { get { return (CipherMode)this.parameters["CipherMode"]; } }

        public PaddingMode PaddingMode { get { return (PaddingMode)this.parameters["PaddingMode"]; } }

        public string Algorithm { get { return (string)this.parameters["Algorithm"]; } }

        public int KeySize { get { return (int)this.parameters["KeySize"]; } }

        public SduEncryptionPolicy(string algorithm, CipherMode cipherMode, int keySize, PaddingMode paddingMode)
        {
            this.parameters["CipherMode"] = cipherMode;
            this.parameters["Algorithm"] = algorithm;
            this.parameters["PaddingMode"] = paddingMode;
            this.parameters["KeySize"] = keySize;
        }

        public override IpcPolicyType PolicyType
        {
            get { return IpcPolicyType.SduEncryptionPolicy; }
        }
    }

    public abstract class SduEncryptionMethod
    {
        protected byte[] _key;
        public abstract byte[] Encrypt(byte[] input);
        public abstract byte[] Decrypt(byte[] input);

        public abstract SduEncryptionPolicy Policy { get; }

        /// <summary>
        /// Gets or sets the key of encryption method. The size of key must be equal or greater than the required key size of the used algorithm.
        /// </summary>
        public byte[] Key { get; set; }
    }

    class SduNullEncryptionMethod : SduEncryptionMethod
    {
        SduEncryptionPolicy _policy = new SduEncryptionPolicy(EncryptionAlgorithm.Null.ToString(), System.Security.Cryptography.CipherMode.ECB, 0, PaddingMode.None);

        public static SduEncryptionMethod Create()
        {
            return new SduNullEncryptionMethod();
        }

        public override byte[] Encrypt(byte[] input)
        {
            return input;
        }

        public override byte[] Decrypt(byte[] input)
        {
            return input;
        }

        public override SduEncryptionPolicy Policy
        {
            get { return this._policy; }
        }
    }
    class SduAesEncryptionMethod : SduEncryptionMethod
    {
        SduEncryptionPolicy _policy;
        public override byte[] Encrypt(byte[] input)
        {
            
            throw new NotImplementedException();
        }

        public override byte[] Decrypt(byte[] input)
        {
            throw new NotImplementedException();
        }

        public override SduEncryptionPolicy Policy
        {
            get { return this._policy; }
        }

        public static SduEncryptionMethod Create(CipherMode cipherMode, int keySize, byte[] key, PaddingMode paddingMode)
        {
            var sm = new SduAesEncryptionMethod();
            sm._policy = new SduEncryptionPolicy(EncryptionAlgorithm.AES.ToString(), cipherMode, keySize, paddingMode);
            sm._key = key;
            return sm;
        }
    }
}
