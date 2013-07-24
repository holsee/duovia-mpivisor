using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor.Management;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

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

        public string Run(string exePath, string args, string payload)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return "Null or empty exePath";
            if (!File.Exists(exePath)) return "exePath does not exist";
            var localExePath = exePath;
            var localArgs = args;
            var localPayload = payload;
            Task.Factory.StartNew(() =>
                {
                    try
                    {
                        //launch the exe with args and payload
                        var info = (null != localArgs && localArgs.Length > 0)
                            ? new ProcessStartInfo(localExePath, string.Join(" ", localArgs))
                            : new ProcessStartInfo(localExePath);
                        info.UseShellExecute = false;
                        info.CreateNoWindow = true;
                        if (!string.IsNullOrWhiteSpace(payload)) info.EnvironmentVariables.Add("payload", payload);
                        Process.Start(info);
                    }
                    catch { }
                });
            return string.Empty;
        }
    }
}
