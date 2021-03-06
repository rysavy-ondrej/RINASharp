using System.IO;
// <copyright file="PipeConnectRequestTest.cs" company="Brno University of Technology">Ondrej Rysavy</copyright>
using System;
using System.Net.Rina.Shims;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using NUnit.Framework;
using System.Net.Rina.Shims.NamedPipes;

namespace System.Net.Rina.Shims.Tests
{
    /// <summary>This class contains parameterized unit tests for PipeConnectRequest</summary>
    [PexClass(typeof(PipeMessageTest))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestFixture]
    public partial class PipeMessageTest
    {

        [TestCase]
        public void ConnectRequestSerializeDeserialize()
        {
            var msg = new PipeConnectRequest()
            {
                SourceAddress = Address.PipeAddressUnc("europe", "dublin"),
                DestinationAddress = Address.PipeAddressUnc("europe", "berlin"),
                SourceApplication = "ipcman",
                DestinationApplication = "ipcman",
                DestinationCepId = 0,
                RequesterCepId = 0
            };
            var stream = new MemoryStream();
            msg.Serialize(stream);
            stream.Position = 0;
            var msgRecv = PipeMessage.Deserialize(stream) as PipeConnectRequest;
            Assert.AreEqual(msg.DestinationAddress.ToString(), msgRecv.DestinationAddress.ToString());
            Assert.AreEqual(msg.DestinationApplication, msgRecv.DestinationApplication);
            Assert.AreEqual(msg.DestinationCepId, msgRecv.DestinationCepId);
            Assert.AreEqual(msg.RequesterCepId, msgRecv.RequesterCepId);
            Assert.AreEqual(msg.SourceAddress.ToString(), msgRecv.SourceAddress.ToString());
            Assert.AreEqual(msg.SourceApplication, msgRecv.SourceApplication);
        }

        [TestCase]
        public void ConnectResponseSerializeDeserialize()
        {
            var msg = new PipeConnectResponse()
            {
                SourceAddress = Address.PipeAddressUnc("europe", "dublin"),
                DestinationAddress = Address.PipeAddressUnc("europe", "berlin"),
                DestinationCepId = 0,
                RequesterCepId = 0,
                ResponderCepId = 0,
                Result = ConnectResult.Rejected                
            };
            var stream = new MemoryStream();
            msg.Serialize(stream);
            stream.Position = 0;
            var msgRecv = PipeMessage.Deserialize(stream) as PipeConnectResponse;
            Assert.AreEqual(msg.DestinationAddress.ToString(), msgRecv.DestinationAddress.ToString());
            Assert.AreEqual(msg.ResponderCepId, msgRecv.ResponderCepId);
            Assert.AreEqual(msg.DestinationCepId, msgRecv.DestinationCepId);
            Assert.AreEqual(msg.RequesterCepId, msgRecv.RequesterCepId);
            Assert.AreEqual(msg.SourceAddress.ToString(), msgRecv.SourceAddress.ToString());
            Assert.AreEqual(msg.Result, msgRecv.Result);
        }
    }
}
