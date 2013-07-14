using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;

namespace DuoVia.MpiVisor.Server.UnitTests
{
    [TestClass]
    public class ClusterServerConfigTests
    {
        [TestMethod]
        public void SerializeClusterServer()
        {
            var config = new ClusterServerConfig();
            config.ClusterServers.Add(new ClusterServerInfo
                {
                    MachineName = "charles",
                    ProcessorCount = 8,
                    EndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("192.168.10.10"), 9989),
                    IsActive = true
                });
            config.ClusterServers.Add(new ClusterServerInfo
            {
                MachineName = "mary",
                ProcessorCount = 4,
                EndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("192.168.10.11"), 9989),
                IsActive = false
            });

            var xml = config.ToXmlString();
            Assert.IsNotNull(xml);

            var parsedConfig = ClusterServerConfig.Parse(xml);
            Assert.IsNotNull(parsedConfig);
            Assert.IsTrue(parsedConfig.ClusterServers.Count == 2);

            parsedConfig.Save();

            var loadedConfig = ClusterServerConfig.Load();
            Assert.IsNotNull(loadedConfig);
            Assert.IsTrue(loadedConfig.ClusterServers.Count == 2);
        }
    }
}