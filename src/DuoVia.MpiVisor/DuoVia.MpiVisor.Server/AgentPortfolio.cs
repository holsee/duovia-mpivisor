using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    internal sealed class AgentPortfolio
    {
        public AgentPortfolio(Guid sessionId)
        {
            this.SessionId = sessionId;
            this.LocalAgentIds = new List<ushort>();
            this.Agents = new Dictionary<ushort, AgentEndPoint>();
        }
        public Guid SessionId { get; set; }
        public Process LocalProcess { get; set; }
        public string LocalProcessAgentName { get; set; }
        public List<ushort> LocalAgentIds { get; set; }
        public Dictionary<ushort, AgentEndPoint> Agents { get; set; }
    }
}
