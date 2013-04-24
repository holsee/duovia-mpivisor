using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    [Serializable]
    public class SpawnRequest
    {
        public Guid SessionId { get; set; }
        public ushort Count { get; set; }
        public string AgentExecutableName { get; set; }
        public byte[] Package { get; set; }
        public string[] Args { get; set; }

        /// <summary>
        /// Only set to true when request is to spawn specific agents on that node. Otherwise this is an original request.
        /// </summary>
        public bool IsVisorDirective { get; set; }

        /// <summary>
        /// Default is 0. Is used to allow Visor to respond to a subsequent request to add even more agents in a running session.
        /// </summary>
        public ushort Offset { get; set; }

        /// <summary>
        /// Used to specify spawn strategy. Default of 0 means no specified strategy.
        /// </summary>
        public int Strategy { get; set; }
    }
}
