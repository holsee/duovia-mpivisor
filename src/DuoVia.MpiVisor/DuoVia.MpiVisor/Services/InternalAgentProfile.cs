using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Used to track running agents in internal node service.
    /// </summary>
    internal sealed class InternalAgentProfile
    {
        public InternalAgentProfile(ushort agentId)
        {
            this.AgentId = agentId;
        }
        public ushort AgentId { get; set; }
        public AppDomain Domain { get; set; }
        public Thread Thread { get; set; }
    }
}
