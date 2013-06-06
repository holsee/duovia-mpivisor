using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class Visor
    {
        public static Agent Connect(bool forceLocal = false)
        {
            return Agent.Connect(forceLocal);
        }

        public static Agent ConnectLocal()
        {
            return Connect(true);
        }

        public static Agent ConnectDistributed()
        {
            return Connect(false);
        }
    }
}
