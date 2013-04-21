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
        void RegisterAgent(Guid sessionId, ushort agentId, string ipAddress, int port);
        void UnRegisterAgent(Guid sessionId, ushort agentId);
        void RegisterClusterNode(string ipAddress, int port);
        void UnRegisterClusterNode(string ipAddress, int port);
        string[] GetRegisteredNodes();
        void RelayMessage(Message message);
        void KillSession(Guid sessionId);
    }
}
