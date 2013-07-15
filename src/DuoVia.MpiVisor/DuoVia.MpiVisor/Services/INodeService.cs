using DuoVia.Net.NamedPipes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Local interface to node service for agent talking to node server or internal service.
    /// </summary>
    public interface INodeService : IDisposable
    {
        int Ping(int echo);
        void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args);
        void SpawnStrategic(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor);
        void Send(Message message);
        void Broadcast(Message message);
        void RegisterMasterAgent(SessionInfo sessionInfo);
        void UnRegisterAgent(Guid sessionId, ushort agentId);
        ushort[] GetRunningAgents(Guid sessionId);
        void KillSession(Guid sessionId);
    }
}
