using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;

namespace DuoVia.MpiVisor.UnitTests
{
    [TestClass]
    public class MessageTests
    {
        [TestMethod]
        public void ConstrutorBuildsMessage()
        {
            var expected = new {
                SessionId = Guid.NewGuid(),
                FromId = (ushort)1,
                ToId = (ushort)2,
                ContentType = 3,
                Content = new { Foo = "Bar" }
            };

            var msg = new Message(expected.SessionId, expected.FromId, expected.ToId, expected.ContentType, expected.Content);

            msg.ShouldHave().AllProperties()
                .But(d => d.IsBroadcast)
                .But(d => d.MessageType)
                .EqualTo(expected);
        }

        [TestMethod]
        public void IsBroadcastShouldReturnTrueWhenBroadcastAgentIdEqualsToId()
        {
             new Message { ToId = MpiConsts.BroadcastAgentId }
                 .IsBroadcast.Should().BeTrue();
        }

        [TestMethod]
        public void IsBroadcastShouldReturnFalseWhenBroadcastAgentIdEqualsToId()
        {
            new Message { ToId = 0 }
                .IsBroadcast.Should().BeFalse();
        }
    }
}