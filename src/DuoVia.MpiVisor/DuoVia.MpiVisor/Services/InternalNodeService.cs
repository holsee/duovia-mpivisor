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
    public sealed class InternalNodeService : INodeService, IDisposable
    {
        public int Ping(int echo)
        {
            return echo;
        }

        public void Spawn(Guid sessionId, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            InternalVisor.Current.Spawn(sessionId, count, agentExecutableName, package, args);
        }

        public void Send(Message message)
        {
            InternalVisor.Current.Send(message);
        }

        public void Broadcast(Message message)
        {
            InternalVisor.Current.Broadcast(message);
        }

        public void RegisterMasterAgent(Guid sessionId)
        {
            InternalVisor.Current.RegisterMasterAgent(sessionId);
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            InternalVisor.Current.UnRegisterAgent(sessionId, agentId);
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            return InternalVisor.Current.GetRunningAgents(sessionId);
        }

        public void Dispose()
        {
            InternalVisor.Current.Dispose();
        }
    }
}
