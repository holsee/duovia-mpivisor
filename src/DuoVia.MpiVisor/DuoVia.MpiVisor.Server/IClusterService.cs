using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    /// <summary>
    /// Services used between cluster node servers.
    /// </summary>
    public interface IClusterService
    {
        void DirectedSpawnRequest(SpawnRequest request);
        void RegisterAgent(SessionInfo sessionInfo, ushort agentId, string ipAddress, int port);
        void UnRegisterAgent(Guid sessionId, ushort agentId);
        void RegisterClusterNode(ClusterServerInfo info);
        void UnRegisterClusterNode(ClusterServerInfo info);
        ClusterServerInfo[] GetRegisteredNodes();
        void RelayMessage(Message message);
        void KillSession(Guid sessionId);
    }
}
