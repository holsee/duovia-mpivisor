using DuoVia.Net.TcpIp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    /// <summary>
    /// Proxy to cluster service.
    /// </summary>
    public sealed class ClusterServiceProxy : TcpClient<IClusterService>, IClusterService
    {
        public ClusterServiceProxy(IPEndPoint endpoint) : base(endpoint) { }

        public void DirectedSpawnRequest(SpawnRequest request)
        {
            Proxy.DirectedSpawnRequest(request);
        }

        public void RegisterAgent(Guid sessionId, ushort agentId, string ipAddress, int port)
        {
            Proxy.RegisterAgent(sessionId, agentId, ipAddress, port);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            Proxy.UnRegisterAgent(sessionId, agentId);
        }

        public void RegisterClusterNode(string ipAddress, int port)
        {
            Proxy.RegisterClusterNode(ipAddress, port);
        }

        public void UnRegisterClusterNode(string ipAddress, int port)
        {
            Proxy.UnRegisterClusterNode(ipAddress, port);
        }

        public string[] GetRegisteredNodes()
        {
            return Proxy.GetRegisteredNodes();
        }

        public void RelayMessage(Message message)
        {
            Proxy.RelayMessage(message);
        }

        //public byte[] GetLogs(Guid sessionId)
        //{
        //    return Proxy.GetLogs(sessionId);
        //}

        public void KillSession(Guid sessionId)
        {
            Proxy.KillSession(sessionId);
        }
    }
}
