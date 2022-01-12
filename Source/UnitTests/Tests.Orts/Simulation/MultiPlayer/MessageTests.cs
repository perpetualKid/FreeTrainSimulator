using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Simulation.MultiPlayer;

namespace Tests.Orts.Simulation.MultiPlayer
{
    [TestClass]
    public class MessageTests
    {
        private static readonly Encoding encoding = Encoding.Unicode;

        [TestMethod]
        public void MsgAliveRoundTripTest()
        {
            ReadOnlySpan<byte> messageSource = encoding.GetBytes("13: ALIVE user123");
            Message message = Message.Decode(messageSource.Slice(8, 10), messageSource[20..]);

            char[] buffer = new char[message.EstimatedMessageSize];
            ReadOnlySpan<char> serializedMessage = message.Serialize(buffer);

            byte[] serializedBytes = new byte[encoding.GetMaxByteCount(message.EstimatedMessageSize)];
            int bytesWritten = encoding.GetBytes(serializedMessage, serializedBytes.AsSpan());

            Assert.IsTrue(MemoryExtensions.SequenceEqual(serializedBytes.AsSpan(0, bytesWritten), messageSource));
        }

    }
}
