using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DuoVia.Net.NamedPipes;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Internal self contained node service for agent use.
    /// </summary>
    public class InternalNodeService : INodeService
    {
        public int Ping(int echo)
        {
            return echo;
        }

        public void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            InternalVisor.Current.Spawn(sessionInfo, count, agentExecutableName, package, args);
        }

        public void SpawnStrategic(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor)
        {
            InternalVisor.Current.Spawn(sessionInfo, count, agentExecutableName, package, args, strategy, factor);
        }

        public void Send(Message message)
        {
            InternalVisor.Current.Send(message);
        }

        public void Broadcast(Message message)
        {
            InternalVisor.Current.Broadcast(message);
        }

        public void RegisterMasterAgent(SessionInfo sessionInfo)
        {
            InternalVisor.Current.RegisterMasterAgent(sessionInfo);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            InternalVisor.Current.UnRegisterAgent(sessionId, agentId);
        }

        public void KillSession(Guid sessionId)
        {
            InternalVisor.Current.KillSession(sessionId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return InternalVisor.Current.GetRunningAgents(sessionId);
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
                    InternalVisor.Current.Dispose();
                }
            }
        }

        #endregion
    }
}
