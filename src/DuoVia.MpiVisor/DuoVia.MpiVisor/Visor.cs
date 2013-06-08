using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class Visor
    {
        public static Agent Connect(string[] args, bool forceLocal = false)
        {
            return Agent.Connect(args, forceLocal);
        }

        public static Agent ConnectLocal(string[] args)
        {
            return Connect(args, true);
        }

        public static Agent ConnectDistributed(string[] args)
        {
            return Connect(args, false);
        }
    }
}
