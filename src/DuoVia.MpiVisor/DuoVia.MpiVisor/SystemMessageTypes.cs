﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class SystemMessageTypes
    {
        public const int Started = -1;
        public const int Stopped = -2;
        public const int Aborted = -3;
        public const int Error = -4;
        public const int Shutdown = -5;
        public const int LogShuttle = -6;
        public const int DeliveryFailure = -7;

        public const int NullMessage = -987654;
        
        //note -999999 is reserved for log message shuttling
    }
}
