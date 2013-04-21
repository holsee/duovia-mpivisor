using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    /// <summary>
    /// Used to track running agents and where those agents are running.
    /// </summary>
    internal class AgentEndPoint
    {
        public AgentEndPoint(Guid sessionId, ushort agentId, IPEndPoint endpoint)
        {
            SessionId = sessionId;
            AgentId = agentId;
            NodeEndPoint = endpoint;
        }

        public Guid SessionId { get; set; }
        public ushort AgentId { get; set; }
        public IPEndPoint NodeEndPoint { get; set; }
        public string Name
        {
            get
            {
                return string.Format("{0}-{1}", AgentId, SessionId);
            }
        }

        public override bool Equals(object obj)
        {
            var compareTo = obj as AgentEndPoint;
            if (null == compareTo) return false;
            return (this.AgentId == compareTo.AgentId 
                && this.SessionId == compareTo.SessionId 
                && IPEndPoint.Equals(this.NodeEndPoint, compareTo.NodeEndPoint));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
