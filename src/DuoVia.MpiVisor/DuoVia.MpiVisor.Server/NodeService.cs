using DuoVia.MpiVisor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DuoVia.MpiVisor.Server
{
    public class NodeService : INodeService
    {
        public int Ping(int echo)
        {
            return echo;
        }

        public void SpawnStrategic(Guid sessionId, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor)
        {
            var request = new SpawnRequest
            {
                SessionId = sessionId,
                Count = count,
                AgentExecutableName = agentExecutableName,
                Package = package,
                Args = args,
                Strategy = strategy,
                Factor = factor
            };
            ServerVisor.Current.EnqueueSpawnRequest(request);
        }

        public void Spawn(Guid sessionId, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            var request = new SpawnRequest 
            { 
                SessionId = sessionId, 
                Count = count, 
                AgentExecutableName = agentExecutableName, 
                Package = package, 
                Args = args 
            };
            ServerVisor.Current.EnqueueSpawnRequest(request);
        }

        public void Send(Message message)
        {
            ServerVisor.Current.EnqueueMessage(message);
        }

        public void Broadcast(Message message)
        {
            message.ToId = MpiConsts.BroadcastAgentId;
            this.Send(message);
        }

        public void RegisterMasterAgent(Guid sessionId)
        {
            ServerVisor.Current.RegisterMasterAgent(sessionId);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            if (agentId == MpiConsts.MasterAgentId)
                ServerVisor.Current.KillSession(sessionId);
            else
                ServerVisor.Current.UnRegisterLocalAgent(sessionId, agentId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return ServerVisor.Current.GetRunningAgents(sessionId);
        }
    }
}
