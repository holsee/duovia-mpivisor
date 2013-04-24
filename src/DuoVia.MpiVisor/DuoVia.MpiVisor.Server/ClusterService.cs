using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuoVia.MpiVisor.Server
{
    /// <summary>
    /// Cluster service provides server service for cluster nodes.
    /// </summary>
    public sealed class ClusterService : IClusterService
    {
        public void DirectedSpawnRequest(SpawnRequest request)
        {
            Visor.Current.EnqueueSpawnRequest(request);
        }

        public void RegisterAgent(Guid sessionId, ushort agentId, string ipAddress, int port)
        {
            Visor.Current.RegisterAgent(sessionId, agentId, ipAddress, port);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            Visor.Current.UnRegisterAgent(sessionId, agentId);            
        }

        public void RegisterClusterNode(ClusterServerInfo info)
        {
            Visor.Current.RegisterClusterNode(info);
        }

        public void UnRegisterClusterNode(ClusterServerInfo info)
        {
            Visor.Current.UnRegisterClusterNode(info);
        }

        public ClusterServerInfo[] GetRegisteredNodes()
        {
            return Visor.Current.GetRegisteredClusterNodes();
        }

        public void RelayMessage(Message message)
        {
            Visor.Current.EnqueueMessage(message);
        }

        public void KillSession(Guid sessionId)
        {
            Visor.Current.KillSessionLocal(sessionId);
        }
    }
}
