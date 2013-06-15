using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DuoVia.MpiVisor
{
    public interface ISessionFactory
    {
        SessionInfo CreateSession(string arguments, out ushort assignedAgentId);
    }
    
    internal class SessionFactory : ISessionFactory
    {
        public SessionInfo CreateSession(string arguments, out ushort assignedAgentId)
        {
            assignedAgentId = 0;
            SessionInfo info;
            // initialize context using app domain data, else create master agent
            var sessionIdData = AppDomain.CurrentDomain.GetData("SessionId");
            var agentIdData = AppDomain.CurrentDomain.GetData("AgentId");
            if (null != sessionIdData && null != agentIdData)
            {
                var id = (Guid)sessionIdData;
                assignedAgentId = (ushort)agentIdData;
                info = new SessionInfo(id, AppDomain.CurrentDomain.FriendlyName.Replace(".exe", string.Empty), arguments);
            }
            else
            {
                //only use environment variables when no app domain data exists to assure that 
                //these are used on the first run of a spawn agent on a given cluster node
                var p = Process.GetCurrentProcess();
                if (p.StartInfo.EnvironmentVariables.ContainsKey("SessionId")
                    && p.StartInfo.EnvironmentVariables.ContainsKey("AgentId"))
                {
                    var sessionIdVar = p.StartInfo.EnvironmentVariables["SessionId"];
                    var agentIdVar = p.StartInfo.EnvironmentVariables["AgentId"];
                    var id = Guid.Parse(sessionIdVar);
                    assignedAgentId = ushort.Parse(agentIdVar);
                    info = new SessionInfo(id, p.ProcessName, arguments);
                }
                else
                {
                    //no domain or environment variables
                    var id = Guid.NewGuid();
                    info = new SessionInfo(id, Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location), arguments);
                }
            }
            return info;
        }
    }
}
