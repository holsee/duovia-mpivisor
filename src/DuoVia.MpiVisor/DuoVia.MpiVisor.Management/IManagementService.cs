using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor.Management
{
    public interface IManagementService
    {
        ManagementInfo GetInfo();
        void KillSession(Guid sessionId);
        string Run(string exePath, string args, string payload);
    }
}
