using DuoVia.Net.NamedPipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Agent service proxy provides access to agent from Visor.
    /// </summary>
    public sealed class AgentServiceProxy : NpClient<IAgentService>, IAgentService
    {
        public AgentServiceProxy(NpEndPoint npEndPoint) : base(npEndPoint) { }

        public int GetChildAgentCount()
        {
            return Proxy.GetChildAgentCount();
        }

        public int Ping(int echo)
        {
            return Proxy.Ping(echo);
        }

        public void Send(Message message)
        {
            Proxy.Send(message);
        }

        public string[] ReadLog()
        {
            return Proxy.ReadLog();
        }

        public void Spawn(ushort agentId, string agentExecutableName, string[] args)
        {
            Proxy.Spawn(agentId, agentExecutableName, args);
        }
    }
}
