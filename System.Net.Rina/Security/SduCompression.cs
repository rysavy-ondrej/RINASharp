using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Net.Rina;
namespace System.Net.Rina.Security
{
    public enum CompressionMethod { Null, Deflate, Zip, GZip }
    public class SduCompressionPolicy : IpcPolicy
    {
        public string Method { get { return (string)this.parameters["Method"]; } }

        public CompressionLevel CompressionLevel { get { return (CompressionLevel)this.parameters["CompressionLevel"]; } }

        public SduCompressionPolicy(string method, CompressionLevel compressionLevel)
        {
            this.parameters["Method"] = method;
            this.parameters["CompressionLevel"] = compressionLevel;
        }

        public override IpcPolicyType PolicyType
        {
            get { return IpcPolicyType.SduCompressionPolicy; }
        }
    }


    public abstract class SduCompressionMethod
    {
        public abstract byte[] Compress(byte[] input);
        public abstract byte[] Decompress(byte[] input);

        public abstract SduCompressionPolicy Policy { get; }

        /// <summary>
        /// Creates SduCompressionMethod object from the specified policy.
        /// </summary>
        /// <param name="policy"></param>
        /// <returns></returns>
        public static SduCompressionMethod Create(SduCompressionPolicy policy)
        {
            var method = (CompressionMethod)Enum.Parse(typeof(CompressionMethod), policy.Method, true);
            switch (method)
            {
                case CompressionMethod.Null: return SduNullCompressionMethod.Create();
                case CompressionMethod.Deflate: return SduDeflateCompressionMethod.Create();
                default: return null;
            }
        }
    }

    class SduNullCompressionMethod : SduCompressionMethod
    {
        SduCompressionPolicy _policy = new SduCompressionPolicy(CompressionMethod.Null.ToString(), CompressionLevel.NoCompression);

        public override byte[] Compress(byte[] input)
        {
            return input;
        }

        public override byte[] Decompress(byte[] input)
        {
            return input;
        }

        public static SduCompressionMethod Create()
        {
            return new SduNullCompressionMethod();
        }

        public override SduCompressionPolicy Policy
        {
            get { 
                return this._policy;                
            }
        }
    }

    /// <summary>
    /// This class implementes compression and decompression functions using Deflate algorithm.
    /// </summary>
    class SduDeflateCompressionMethod : SduCompressionMethod
    {
        SduCompressionPolicy _policy = new SduCompressionPolicy(CompressionMethod.Deflate.ToString(), CompressionLevel.Fastest);

        public override byte[] Compress(byte[] input)
        {            
            var ms = new MemoryStream(input.Length/2);
            using(var ds = new DeflateStream(ms, CompressionMode.Compress))
            {
                ds.Write(input, 0, input.Length);
            }
            return ms.ToArray();
        }

        public override byte[] Decompress(byte[] input)
        {
            var ms = new MemoryStream(input.Length);
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                ds.Write(input, 0, input.Length);
            }
            return ms.ToArray();
        }

        public override SduCompressionPolicy Policy
        {
            get { return this._policy; }
        }

        public static SduCompressionMethod Create()
        {
            return new SduDeflateCompressionMethod(); 
        }
    }
}
