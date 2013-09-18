using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.NamedPipes;
using DuoVia.Net.TcpIp;
using System.Net;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Local node service proxy.
    /// </summary>
    public sealed class NodeServiceProxy : NpClient<INodeService>, INodeService
    {
        public NodeServiceProxy(NpEndPoint npEndPoint) : base (npEndPoint) { }

        public int Ping(int echo)
        {
            return Proxy.Ping(echo);
        }

        public void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            Proxy.Spawn(sessionInfo, count, agentExecutableName, package, args);
        }

        public void SpawnStrategic(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor)
        {
            Proxy.SpawnStrategic(sessionInfo, count, agentExecutableName, package, args, strategy, factor);
        }

        public void Send(Message message)
        {
            Proxy.Send(message);
        }

        public void Broadcast(Message message)
        {
            Proxy.Broadcast(message);
        }

        public void RegisterMasterAgent(SessionInfo sessionInfo)
        {
            Proxy.RegisterMasterAgent(sessionInfo);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            Proxy.UnRegisterAgent(sessionId, agentId);
        }

        public void KillSession(Guid sessionId)
        {
            Proxy.KillSession(sessionId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return Proxy.GetRunningAgents(sessionId);
        }
    }
}
