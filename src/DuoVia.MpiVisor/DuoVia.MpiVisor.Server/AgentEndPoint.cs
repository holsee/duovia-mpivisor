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
        public AgentEndPoint(SessionInfo sessionInfo, ushort agentId, IPEndPoint endpoint)
        {
            Session = sessionInfo;
            AgentId = agentId;
            NodeEndPoint = endpoint;
        }

        public SessionInfo Session { get; set; }
        public ushort AgentId { get; set; }
        public IPEndPoint NodeEndPoint { get; set; }
        public string Name
        {
            get
            {
                return string.Format("{0}-{1}", AgentId, Session.SessionId);
            }
        }

        public override bool Equals(object obj)
        {
            var compareTo = obj as AgentEndPoint;
            if (null == compareTo) return false;
            return (this.AgentId == compareTo.AgentId 
                && this.Session.SessionId == compareTo.Session.SessionId 
                && IPEndPoint.Equals(this.NodeEndPoint, compareTo.NodeEndPoint));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
