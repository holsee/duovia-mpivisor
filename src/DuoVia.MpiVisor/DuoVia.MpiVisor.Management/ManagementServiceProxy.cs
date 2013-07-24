using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.TcpIp;
using System.Net;

namespace DuoVia.MpiVisor.Management
{
    public class ManagementServiceProxy : TcpClient<IManagementService>, IManagementService
    {
        public ManagementServiceProxy(IPEndPoint endpoint) : base(endpoint) { }

        public ManagementInfo GetInfo()
        {
            return Proxy.GetInfo();
        }

        public void KillSession(Guid sessionId)
        {
            Proxy.KillSession(sessionId);
        }

        public string Run(string exePath, string args, string payload)
        {
            return Proxy.Run(exePath, args, payload);
        }
    }
}
