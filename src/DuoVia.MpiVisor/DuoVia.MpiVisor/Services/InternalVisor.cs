using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DuoVia.Net.NamedPipes;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Singleton provides Visor function for self contained internal node service.
    /// </summary>
    public sealed class InternalVisor : IDisposable
    {
        private static readonly InternalVisor _current = new InternalVisor();
        private ManualResetEvent _outgoingMessageWaitHandle = new ManualResetEvent(false);
        private LinkedList<Message> _outgoingMessageBuffer = new LinkedList<Message>();
        private Thread _sendMessagesThread = null;
        private bool _continueSendingMessages = true;
        private Dictionary<ushort, InternalAgentProfile> _agentProfiles = new Dictionary<ushort, InternalAgentProfile>();

        private InternalVisor()
        {
            //start SendMessages thread
            _sendMessagesThread = new Thread(SendMessages);
            _sendMessagesThread.IsBackground = true;
            _sendMessagesThread.Start();
        }

        public static InternalVisor Current
        {
            get
            {
                return _current;
            }
        }

        private void SendMessages(object state)
        {
            while (_continueSendingMessages)
            {
                _outgoingMessageWaitHandle.WaitOne();
                Message message = null;
                lock (_outgoingMessageBuffer)
                {
                    LinkedListNode<Message> firstMessage = _outgoingMessageBuffer.First;
                    if (firstMessage != null)
                    {
                        message = firstMessage.Value;
                        _outgoingMessageBuffer.RemoveFirst();
                    }
                    else
                    {
                        //set to nonsignaled and block on WaitOne again
                        _outgoingMessageWaitHandle.Reset();
                    }
                }
                if (null != message)
                {
                    var toAgentIds = new List<ushort>();
                    if (message.IsBroadcast)
                    {
                        lock (_agentProfiles)
                        {
                            var agentIds = (from n in _agentProfiles where n.Key != message.FromId select n.Key).ToList();
                        }
                    }
                    else
                    {
                        toAgentIds.Add(message.ToId);
                    }
                    foreach (var agentId in toAgentIds)
                    {
                        try
                        {
                            var agentName = GetAgentName(agentId, message.SessionId);
                            using (var proxy = new AgentServiceProxy(new NpEndPoint(agentName, 2500)))
                            {
                                proxy.Send(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            SendFailedDeliveryMessage(message);
                            Log.Error("message send error: {0}", ex);
                        }
                    }
                }
            }
        }

        private void SendFailedDeliveryMessage(Message originalMessage)
        {
            if (originalMessage.MessageType != SystemMessageTypes.DeliveryFailure)
            {
                //reverse direction of original message - from is to and to is from
                Send(new Message(originalMessage.SessionId, originalMessage.ToId, originalMessage.FromId, SystemMessageTypes.DeliveryFailure, originalMessage));
            }
        }

        public void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args, int strategy, double factor)
        {
            ushort agentsPerNode = 1;
            switch(strategy)
            {
                case 1:
                    //spawn one agent per cluster visor node - force count to 1
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args); 
                    break;
                case 2:
                    //spawn one agent per cluster visor node cpu/core (logical processors)
                    agentsPerNode = (ushort)Environment.ProcessorCount;
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args);
                    break;
                case 3:
                    //spawn one agent per cluster visor node cpu/core (logical processors) - 1
                    agentsPerNode = (ushort)(Environment.ProcessorCount - 1);
                    if (agentsPerNode < 1) agentsPerNode = 1;
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args);
                    break;
                case 4:
                    //spawn one agent per cluster visor node cpu/core (logical processors) - (int)factor
                    if (factor < 0.0) factor = 0.0;
                    if (factor > Environment.ProcessorCount) factor = Environment.ProcessorCount - 1.0;
                    agentsPerNode = (ushort)(Environment.ProcessorCount - (int)factor);
                    if (agentsPerNode < 1) agentsPerNode = 1;
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args);
                    break;
                case 5:
                    //spawn one agent per cluster visor node cpu/core (logical processors) * factor (percentage)
                    if (factor < 0.0) factor = 0.1;
                    if (factor > Environment.ProcessorCount * 10.0) factor = Environment.ProcessorCount * 10.0;
                    agentsPerNode = (ushort)(Environment.ProcessorCount * factor);
                    if (agentsPerNode < 1) agentsPerNode = 1;
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args);
                    break;
                case 6:
                    //spawn (int)factor agents per cluster visor node cpu/core 
                    if (factor < 0.0) factor = 1.0;
                    if (factor > Environment.ProcessorCount * 10.0) factor = Environment.ProcessorCount * 10.0;
                    agentsPerNode = (ushort)((int)factor);
                    if (agentsPerNode < 1) agentsPerNode = 1;
                    Spawn(sessionInfo, agentsPerNode, agentExecutableName, package, args);
                    break;
                default:
                    //default standard behavior
                    Spawn(sessionInfo, count, agentExecutableName, package, args); 
                    break;
            }
        }

        public void Spawn(SessionInfo sessionInfo, ushort count, string agentExecutableName, byte[] package, string[] args)
        {
            Task.Factory.StartNew(() =>
                {
                    lock (_agentProfiles)
                    {
                        //must allow multiple calls to spawn
                        if (_agentProfiles.Count + count >= ushort.MaxValue) throw new ArgumentException("total max agents will be exceeded", "count");
                        ushort agentOffest = (ushort)(_agentProfiles.Count);
                        //for local packageFileLocation does not exist - just use existing binaries in place
                        var basePath = AppDomain.CurrentDomain.BaseDirectory;
                        var assemblyLocation = Path.Combine(basePath, agentExecutableName);
                        var configFile = assemblyLocation + ".config";
                        var configExists = File.Exists(configFile);
                        for (ushort i = agentOffest; i < agentOffest + count; i++)
                        {
                            try
                            {
                                ushort agentId = i; //scope copy
                                var agentName = GetAgentName(agentId, sessionInfo.SessionId);
                                var setup = new AppDomainSetup();
                                setup.ApplicationBase = basePath;
                                if (configExists) setup.ConfigurationFile = configFile;
                                var domain = AppDomain.CreateDomain(agentName, null, setup);

                                domain.SetData("SessionId", sessionInfo.SessionId);
                                domain.SetData("AgentId", agentId);

                                //execute agent on new thread
                                var domainThread = new Thread(() =>
                                    {
                                        try
                                        {
                                            domain.ExecuteAssembly(assemblyLocation, args);
                                        }
                                        catch (Exception tx)
                                        {
                                            Log.Error("Agent {0} unhandled exception: {1}", agentId, tx);
                                            this.Send(new Message
                                            {
                                                ToId = MpiConsts.MasterAgentId,
                                                SessionId = sessionInfo.SessionId,
                                                FromId = agentId,
                                                MessageType = SystemMessageTypes.Aborted,
                                                Content = tx.ToString()
                                            });
                                        }
                                    })
                                {
                                    IsBackground = true
                                };
                                domainThread.Start();
                                _agentProfiles.Add(agentId, new InternalAgentProfile(agentId) 
                                    { 
                                        Domain = domain,
                                        Thread = domainThread
                                    });
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Spawn error: {0}", ex);
                            }
                        }
                    }
                }
                , TaskCreationOptions.LongRunning);
        }

        private string GetAgentName(ushort agentId, Guid sessionId)
        {
            return string.Format("{0}-{1}", agentId, sessionId);
        }

        public void Send(Message message)
        {
            lock (_outgoingMessageBuffer)
            {
                _outgoingMessageBuffer.AddLast(message);
                _outgoingMessageWaitHandle.Set(); //signal new messages
            }
        }

        public void Broadcast(Message message)
        {
            //assures is sent to all agents except sender
            message.ToId = MpiConsts.BroadcastAgentId; 
            Send(message);
        }

        public void RegisterMasterAgent(SessionInfo sessionInfo)
        {
            lock (_agentProfiles)
            {
                _agentProfiles.Add(MpiConsts.MasterAgentId, new InternalAgentProfile(MpiConsts.MasterAgentId)
                    {
                        Domain = AppDomain.CurrentDomain,
                        Thread = Thread.CurrentThread
                    });
            }
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            lock (_agentProfiles)
            {
                if (_agentProfiles.ContainsKey(agentId)) _agentProfiles.Remove(agentId);
            }
        }

        public void KillSession(Guid sessionId)
        {
            lock (_agentProfiles)
            {
                foreach (var agent in _agentProfiles)
                {
                    try
                    {
                        AppDomain.Unload(agent.Value.Domain);
                    }
                    catch (Exception e)
                    {
                        Log.Error("agent kill local {0}", e);
                    }
                }
            }
            _continueSendingMessages = false;
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            lock (_agentProfiles)
            {
                return _agentProfiles.Keys.ToArray();
            }
        }

        public void Dispose()
        {
            _continueSendingMessages = false;
            _outgoingMessageWaitHandle.Dispose();
        }
    }
}
