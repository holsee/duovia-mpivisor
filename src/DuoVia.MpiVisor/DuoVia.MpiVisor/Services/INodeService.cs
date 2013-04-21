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
    public interface INodeService
    {
        int Ping(int echo);
        void Spawn(Guid sessionId, ushort count, string agentExecutableName, byte[] package, string[] args);
        void Send(Message message);
        void Broadcast(Message message);
        //void BeginLogFileConsolidation(Guid sessionId, string destinationFileName);
        void RegisterMasterAgent(Guid sessionId);
        void UnRegisterAgent(Guid sessionId, ushort agentId);
        ushort[] GetRunningAgents(Guid sessionId);
    }
}
