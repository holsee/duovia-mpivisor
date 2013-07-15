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

        public void SpawnStrategic(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor)
        {
            var request = new SpawnRequest
            {
                Session = sessionInfo,
                Count = count,
                AgentExecutableName = agentExecutableName,
                Package = package,
                Args = args,
                Strategy = strategy,
                Factor = factor
            };
            ServerVisor.Current.EnqueueSpawnRequest(request);
        }

        public void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            var request = new SpawnRequest 
            {
                Session = sessionInfo, 
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

        public void RegisterMasterAgent(SessionInfo sessionInfo)
        {
            ServerVisor.Current.RegisterMasterAgent(sessionInfo);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            ServerVisor.Current.UnRegisterLocalAgent(sessionId, agentId);
        }

        public void KillSession(Guid sessionId)
        {
            ServerVisor.Current.KillSession(sessionId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return ServerVisor.Current.GetRunningAgents(sessionId);
        }

        #region IDisposable members

        private bool _disposed = false;

        public void Dispose()
        {
            //MS recommended dispose pattern - prevents GC from disposing again
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true; //prevent second call to Dispose
                if (disposing)
                {
                }
            }
        }

        #endregion

    }
}
