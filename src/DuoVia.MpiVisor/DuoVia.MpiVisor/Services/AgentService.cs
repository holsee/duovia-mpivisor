using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Agent service interface for visor calling agent.
    /// </summary>
    public interface IAgentService
    {
        int Ping(int echo);
        void Send(Message message);
        string[] ReadLog();
        void Spawn(ushort agentId, string agentExecutableName, string[] args);
        int GetChildAgentCount();
    }
    
    /// <summary>
    /// Implemenents service on agent side called by visor.
    /// </summary>
    public class AgentService : IAgentService
    {
        private List<Task> _runningAgentTasks = new List<Task>();

        public int GetChildAgentCount()
        {
            lock (_runningAgentTasks)
            {
                var count = (from n in _runningAgentTasks where n.IsAlive() select n).Count();
                return count;
            }
        }

        public int Ping(int echo)
        {
            return echo;
        }

        public void Send(Message message)
        {
            ((IMessageQueueWriter)Agent.Current.MessageQueue).EnqueuMessage(message);
        }

        public string[] ReadLog()
        {
            return Log.ReadFile();
        }

        public void Spawn(ushort agentId, string agentExecutableName, string[] args)
        {
            lock (_runningAgentTasks)
            {
                try
                {
                    if (null == Agent.Current) throw new Exception("No agent context");
                    var sessionId = Agent.Current.Session.SessionId;
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    var assemblyLocation = Path.Combine(basePath, agentExecutableName);
                    var configFile = assemblyLocation + ".config";
                    var configExists = File.Exists(configFile);
                    var agentName = GetAgentName(agentId, sessionId);
                    var setup = new AppDomainSetup();
                    setup.ApplicationBase = basePath;
                    if (configExists) setup.ConfigurationFile = configFile;
                    var domain = AppDomain.CreateDomain(agentName, null, setup);

                    domain.SetData("SessionId", sessionId);
                    domain.SetData("AgentId", agentId);

                    //execute agent on new task
                    var task = Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                domain.ExecuteAssembly(assemblyLocation, args);
                            }
                            catch (Exception tx)
                            {
                                Log.Error("Agent {0} unhandled exception: {1}", agentId, tx);
                                Agent.Current.MessageQueue.Send(new Message
                                {
                                    ToId = MpiConsts.MasterAgentId,
                                    SessionId = sessionId,
                                    FromId = agentId,
                                    MessageType = SystemMessageTypes.Aborted,
                                    Content = tx.ToString()
                                });
                            }
                        });
                    _runningAgentTasks.Add(task);
                }
                catch (Exception e)
                {
                    Log.Error("failed to spawn new agent {0}: {1}", agentId, e);
                }
            }
        }

        private string GetAgentName(ushort agentId, Guid sessionId)
        {
            return string.Format("{0}-{1}", agentId, sessionId);
        }
    }
}
