using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class SpawnStrategy
    {
        public static readonly int None = 0;
        public static readonly int OneAgentPerNode = 1;
        public static readonly int OneAgentPerLogicalProcessor = 2;
        public static readonly int OneAgentPerLogicalProcessorLessOne = 3;
        public static readonly int OneAgentPerLogicalProcessorLessCount = 4;
        public static readonly int OneAgentAsFactorOfLogicalProcessorCount = 5;
        public static readonly int CountOfAgentsPerNode = 6;
    }
}
