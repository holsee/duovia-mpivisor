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

        public void RegisterClusterNode(string ipAddress, int port)
        {
            Visor.Current.RegisterClusterNode(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        }

        public void UnRegisterClusterNode(string ipAddress, int port)
        {
            Visor.Current.UnRegisterClusterNode(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        }

        public string[] GetRegisteredNodes()
        {
            var nodes = Visor.Current.GetRegisteredClusterNodes();
            List<string> list = new List<string>();
            foreach (var node in nodes)
            {
                list.Add(node.Address.ToString() + "," + node.Port.ToString());
            }
            return list.ToArray();
        }

        public void RelayMessage(Message message)
        {
            Visor.Current.EnqueueMessage(message);
        }

        //public byte[] GetLogs(Guid sessionId)
        //{
        //    return Visor.Current.PackageLogs(sessionId);
        //}

        public void KillSession(Guid sessionId)
        {
            Visor.Current.KillSessionLocal(sessionId);
        }
    }
}
