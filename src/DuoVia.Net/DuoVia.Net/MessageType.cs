using System;
using System.Collections.Generic;
using System.Text;

namespace DuoVia.Net
{
    internal enum MessageType
    {
        TerminateConnection = 0,
        MethodInvocation = 1,
        ReturnValues = 2,
        UnknownMethod = 3,
        ThrowException = 4,
        SyncInterface = 5
    };
}
