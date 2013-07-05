using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using DuoVia.Net.TcpIp;

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

        public void RegisterAgent(SessionInfo sessionInfo, ushort agentId, string ipAddress, int port)
        {
            Proxy.RegisterAgent(sessionInfo, agentId, ipAddress, port);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            Proxy.UnRegisterAgent(sessionId, agentId);
        }

        public void RegisterClusterNode(ClusterServerInfo info)
        {
            Proxy.RegisterClusterNode(info);
        }

        public void UnRegisterClusterNode(ClusterServerInfo info)
        {
            Proxy.UnRegisterClusterNode(info);
        }

        public ClusterServerInfo[] GetRegisteredNodes()
        {
            return Proxy.GetRegisteredNodes();
        }

        public void RelayMessage(Message message)
        {
            Proxy.RelayMessage(message);
        }

        public void KillSession(Guid sessionId)
        {
            Proxy.KillSession(sessionId);
        }
    }
}
