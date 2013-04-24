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

        public void SpawnStrategic(Guid sessionId, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy)
        {
            var request = new SpawnRequest
            {
                SessionId = sessionId,
                Count = count,
                AgentExecutableName = agentExecutableName,
                Package = package,
                Args = args,
                Strategy = strategy
            };
            Visor.Current.EnqueueSpawnRequest(request);
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
            Visor.Current.EnqueueSpawnRequest(request);
        }

        public void Send(Message message)
        {
            Visor.Current.EnqueueMessage(message);
        }

        public void Broadcast(Message message)
        {
            message.ToId = MpiConsts.BroadcastAgentId;
            this.Send(message);
        }

        //public void BeginLogFileConsolidation(Guid sessionId, string destinationFileName)
        //{
        //    Visor.Current.EnqueueLogConsolidationRequest(
        //        new LogConsolidationRequest { SessionId = sessionId, DestinationFileName = destinationFileName });
        //}

        public void RegisterMasterAgent(Guid sessionId)
        {
            Visor.Current.RegisterMasterAgent(sessionId);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            if (agentId == MpiConsts.MasterAgentId)
                Visor.Current.KillSession(sessionId);
            else
                Visor.Current.UnRegisterLocalAgent(sessionId, agentId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return Visor.Current.GetRunningAgents(sessionId);
        }
    }
}
