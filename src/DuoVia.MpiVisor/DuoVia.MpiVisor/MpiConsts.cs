using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class MpiConsts
    {
        public static readonly string NodeServicePipeName = "MpiVisorNodeService";
        public static readonly ushort MasterAgentId = ushort.MinValue;
        public static readonly ushort BroadcastAgentId = ushort.MaxValue;
    }
}
