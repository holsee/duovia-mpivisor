using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor.Management;

namespace DuoVia.MpiVisor.Server
{
    public class ManagementService : IManagementService
    {
        public ManagementInfo GetInfo()
        {
            return ServerVisor.Current.GetManagementInfo();
        }

        public void KillSession(Guid sessionId)
        {
            ServerVisor.Current.KillSession(sessionId);
        }
    }
}
