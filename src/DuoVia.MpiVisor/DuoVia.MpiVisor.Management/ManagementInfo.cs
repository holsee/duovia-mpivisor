using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.MpiVisor.Management
{
    [Serializable]
    public class ManagementInfo
    {
        public NodeInfo[] Nodes { get; set; }
        public SessionSummary[] Sessions { get; set; }
    }

    [Serializable]
    public class NodeInfo
    {
        public IPEndPoint EndPoint { get; set; }
        public ushort ProcessorCount { get; set; }
        public string MachineName { get; set; }
    }

    [Serializable]
    public class SessionSummary
    {
        private DateTime _created;
        private Guid _sessionId;
        private string _processName;
        private string _arguments;
        private Dictionary<IPEndPoint, ushort> _agentCounts;

        public SessionSummary()
        {
        }

        public SessionSummary(Guid sessionId, string processName, string arguments, DateTime createdUtc, Dictionary<IPEndPoint, ushort> agentCounts)
        {
            _sessionId = sessionId;
            _processName = processName;
            _arguments = arguments;
            _created = createdUtc;
            _agentCounts = agentCounts;
        }

        public Guid SessionId { get { return _sessionId; } }
        public string ProcessName { get { return _processName; } }
        public string Arguments { get { return _arguments; } }
        public DateTime CreatedUtc { get { return _created; } }
    }


}
