using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    [Serializable]
    public class SessionInfo
    {
        private DateTime _created;
        private Guid _sessionId;
        private string _processName;
        private string _arguments;

        public SessionInfo() 
        {
            _created = DateTime.UtcNow;
        }
        
        public SessionInfo(Guid sessionId, string processName, string arguments)
        {
            _sessionId = sessionId;
            _processName = processName;
            _arguments = arguments;
            _created = DateTime.UtcNow;
        }
        
        public Guid SessionId { get { return _sessionId; }  }
        public string ProcessName { get { return _processName; } }
        public string Arguments { get { return _arguments; } }
        public DateTime CreatedUtc { get { return _created; } }
    }
}
