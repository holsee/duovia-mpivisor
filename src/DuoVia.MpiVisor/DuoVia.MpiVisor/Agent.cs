using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor.Services;
using DuoVia.Net.NamedPipes;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;

namespace DuoVia.MpiVisor
{
    public sealed class Agent : IDisposable
    {
        //singleton instance
        private static Agent _current = null;

        //node service factory and client proxy - allows agent to talk to node service
        private readonly NodeServiceFactory _nodeServiceFactory = null;
        private readonly INodeService _nodeServiceProxy = null;
        
        //agent service host and implementation - allows node service to talk to agent
        private readonly AgentService _agentService = null;
        private readonly NpHost _agentServiceHost = null;
        private readonly DateTime _startedAt = DateTime.Now;

        //incoming message queue and reset event to signal blocking receive message methods
        //the EnqueuMessage method is called by the thread running the agent service which
        //signals the event so that the receive message method can continue and return the message
        private ManualResetEvent _incomingMessageWaitHandle = new ManualResetEvent(false);
        private LinkedList<Message> _incomingMessageBuffer = new LinkedList<Message>();
        private bool continueReceiving = true;

        //used to prevent execution of Dispose code twice which would throw an exception
        private bool isDisposed = false;

        //determines how long a primary agent (the process) will wait for a child agent (app domain)
        //to complete and dispose before completing the dispose of the primary agent
        private const int maxMinutesWaitForChildAgentCompletion = 20;

        //singleton - cannot create instance publicly - this is called by the Connect method
        private Agent(bool useInternalNodeService = false)
        {
            // initialize context using app domain data, else create master agent
            var sessionIdData = AppDomain.CurrentDomain.GetData("SessionId");
            var agentIdData = AppDomain.CurrentDomain.GetData("AgentId");
            if (null != sessionIdData && null != agentIdData)
            {
                SessionId = (Guid)sessionIdData;
                AgentId = (ushort)agentIdData;
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
                    SessionId = Guid.Parse(sessionIdVar);
                    AgentId = ushort.Parse(agentIdVar);
                }
                else
                {
                    //no domain or environment variables
                    SessionId = Guid.NewGuid();
                    AgentId = 0;
                }
            }

            //create connection to NodeService 
            _nodeServiceFactory = new NodeServiceFactory();
            _nodeServiceProxy = _nodeServiceFactory.CreateConnection(this.Name, useInternalNodeService); 

            //open AgentService host to allow server to talk to this agent
            //pipeName is always agent Name to allow multiple agent instances on same machine
            _agentService = new AgentService();
            _agentServiceHost = new NpHost(_agentService, this.Name); 
            _agentServiceHost.Open();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            try
            {
                if (!isDisposed) this.Dispose(); //attempt to execute dispose logic if not already disposed
            }
            catch { }
        } 

        /// <summary>
        /// Call to create and connect current agent context. Required to use Current agent context.
        /// </summary>
        /// <param name="forceLocal"></param>
        /// <returns></returns>
        internal static Agent Connect(bool forceLocal = false)
        {
            if (null == _current) //only connect once
            {
                _current = new Agent(forceLocal);
                if (_current.IsMaster)
                {
                    _current._nodeServiceProxy.RegisterMasterAgent(_current.SessionId);
                }
                else
                {
                    //spawned agents send started message back to master agent
                    _current.Send(new Message(_current.SessionId,
                        _current.AgentId, MpiConsts.MasterAgentId, SystemMessageTypes.Started, null));
                }
            }
            return _current;
        }

        /// <summary>
        /// The current agent context used to interact with Visor.
        /// </summary>
        public static Agent Current { get { return _current; } }

        /// <summary>
        /// The unique agent id.
        /// </summary>
        public ushort AgentId { get; set; }

        /// <summary>
        /// The unique session id for this instance of the application execution context.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Simple way to determine if the agent id is 0.
        /// </summary>
        public bool IsMaster { get { return (AgentId == 0); } }

        /// <summary>
        /// The name of this agent instance. Combines agent id and session id.
        /// </summary>
        public string Name { get { return string.Format("{0}-{1}", AgentId, SessionId); } }

        /// <summary>
        /// Spawn worker agents from master agent. Only master agent can spawn worker agents.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="args"></param>
        public void SpawnAgents(ushort count, string[] args)
        {
            SpawnInternal(count, args, 0, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneAgentPerNode(string[] args)
        {
            SpawnInternal(1, args, 1, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per logical processor on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneAgentPerLogicalProcessor(string[] args)
        {
            SpawnInternal(1, args, 2, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per logical processor, less one, on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneAgentPerLogicalProcessorLessOne(string[] args)
        {
            SpawnInternal(1, args, 3, 0.0);
        }
        /// <summary>
        /// Spawn one worker agent per logical processor, less one, on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="count">agents = LogicalProcessCount - count</param>
        public void SpawnOneAgentPerLogicalProcessorLessCount(string[] args, int count)
        {
            SpawnInternal(1, args, 4, Convert.ToDouble(count));
        }
        /// <summary>
        /// Spawn worker agents by factor multiplied by number of logical processors on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="factor">agents = LogicalProcessCount * factor</param>
        public void SpawnOneAgentAsFactorOfLogicalProcessorCount(string[] args, double factor)
        {
            SpawnInternal(1, args, 5, factor);
        }
        /// <summary>
        /// Spawn count of worker agents on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="count">agents = count</param>
        public void SpawnCountOfAgentsPerNode(string[] args, int count)
        {
            SpawnInternal(1, args, 6, Convert.ToDouble(count));
        }


        private void SpawnInternal(ushort count, string[] args, int strategy, double factor)
        {
            if (!IsMaster) throw new Exception("Only master agent can spawn new agents");
            if (count == MpiConsts.MasterAgentId || count == MpiConsts.BroadcastAgentId)
            {
                throw new ArgumentException("Count must be between 1 and 65,534", "count");
            }
            var package = _nodeServiceFactory.IsInternalServer ? new byte[0] : ZipUtils.PackageAgent();
            var entryAssembly = Assembly.GetEntryAssembly();
            var agentExecutableName = Path.GetFileName(entryAssembly.Location);
            if (strategy > 0)
                _nodeServiceProxy.SpawnStrategic(this.SessionId, count, agentExecutableName, package, args, strategy, factor);
            else
                _nodeServiceProxy.Spawn(this.SessionId, count, agentExecutableName, package, args);
        }

        /// <summary>
        /// Broadcast message to all other running agents.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="content"></param>
        public void Broadcast(int messageType, object content)
        {
            var msg = new Message
            {
                FromId = AgentId,
                SessionId = SessionId,
                ToId = MpiConsts.BroadcastAgentId,
                MessageType = messageType,
                Content = content
            };
            _nodeServiceProxy.Broadcast(msg);
        }

        /// <summary>
        /// Send message to another worker agent in this execution context session.
        /// </summary>
        /// <param name="message"></param>
        public void Send(Message message)
        {
            _nodeServiceProxy.Send(message);
        }

        /// <summary>
        /// Send message to another agent from this agent.
        /// </summary>
        /// <param name="toAgentId"></param>
        /// <param name="messageType"></param>
        /// <param name="content"></param>
        public void Send(ushort toAgentId, int messageType, object content)
        {
            var msg = new Message
            {
                FromId = AgentId,
                SessionId = SessionId,
                ToId = toAgentId,
                MessageType = messageType,
                Content = content
            };
            Send(msg);
        }

        /// <summary>
        /// Returns first message in queue when received. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.</param>
        /// <returns></returns>
        public Message ReceiveAnyMessage(int timeoutSeconds = int.MaxValue)
        {
            var entered = DateTime.Now;
            while (continueReceiving && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
                Message message = null;
                lock (_incomingMessageBuffer)
                {
                    LinkedListNode<Message> firstMessage = _incomingMessageBuffer.First;
                    if (firstMessage != null)
                    {
                        message = firstMessage.Value;
                        _incomingMessageBuffer.RemoveFirst();
                    }
                    else
                    {
                        //set to nonsignaled and block on WaitOne again
                        _incomingMessageWaitHandle.Reset();
                    }
                }
                if (null != message)
                {
                    return message;
                }
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from a specified agent. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="fromAgentId">The agent id from whom a message is expected.</param>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// Since this is filtered, the default is on 90 seconds.</param>
        /// <returns></returns>
        public Message ReceiveFilteredMessage(ushort fromAgentId, int timeoutSeconds = 90)
        {
            var entered = DateTime.Now;
            while (continueReceiving && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
                Message message = null;
                lock (_incomingMessageBuffer)
                {
                    LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                    while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                    {
                        if (nodeMessage.Value.FromId == fromAgentId)
                        {
                            message = nodeMessage.Value;
                            _incomingMessageBuffer.Remove(nodeMessage);
                        }
                        else
                            nodeMessage = nodeMessage.Next;
                    }
                    if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                }
                if (null != message)
                {
                    return message;
                }
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from for a specified content type. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="contentType">The content type of the message expected.</param>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// Since this is filtered, the default is on 90 seconds.</param>
        /// <returns></returns>
        public Message ReceiveFilteredMessage(int contentType, int timeoutSeconds = 90)
        {
            var entered = DateTime.Now;
            while (continueReceiving && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
                Message message = null;
                lock (_incomingMessageBuffer)
                {
                    LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                    while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                    {
                        if (nodeMessage.Value.MessageType == contentType)
                        {
                            message = nodeMessage.Value;
                            _incomingMessageBuffer.Remove(nodeMessage);
                        }
                        else
                            nodeMessage = nodeMessage.Next;
                    }
                    if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                }
                if (null != message)
                {
                    return message;
                }
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from for an application level content type. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// If no message received in timeoutSeconds, a null be returned.</param>
        /// <returns></returns>
        public Message ReceiveApplicationMessage(int timeoutSeconds = int.MaxValue)
        {
            var entered = DateTime.Now;
            while (continueReceiving && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
                Message message = null;
                lock (_incomingMessageBuffer)
                {
                    LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                    while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                    {
                        if (nodeMessage.Value.MessageType > -1)
                        {
                            message = nodeMessage.Value;
                            _incomingMessageBuffer.Remove(nodeMessage);
                        }
                        else
                            nodeMessage = nodeMessage.Next;
                    }
                    if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                }
                if (null != message)
                {
                    return message;
                }
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Create the message that is returned when Receive methods timeout.
        /// </summary>
        /// <returns></returns>
        private Message MakeNullMessage()
        {
            //make a message that is from and to this agent with unused message type value
            var msg = new Message(SessionId, AgentId, AgentId, -987654, null);
            return msg;
        }

        /// <summary>
        /// Get the list of agent ids that are currently running.
        /// </summary>
        /// <returns></returns>
        public ushort[] GetRunningAgents()
        {
            return _nodeServiceProxy.GetRunningAgents(this.SessionId);
        }

        /// <summary>
        /// Adds message to incoming queue to be "received". 
        /// Called by agent service to deliver messages.
        /// </summary>
        /// <param name="message"></param>
        internal void EnqueuMessage(Message message)
        {
            lock (_incomingMessageBuffer)
            {
                if (continueReceiving)
                {
                    if (message.MessageType == -999999)
                    {
                        if (message.Content != null) Log.LogMessage(message.Content.ToString());
                    }
                    else
                    {
                        _incomingMessageBuffer.AddLast(message);
                        _incomingMessageWaitHandle.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Dispose of resources and send proper message. 
        /// Spawned agent process will wait for child agents before cleaning up.
        /// </summary>
        public void Dispose()
        {
            //prevent dispose being called twice
            isDisposed = true;

            //tell message receiving thread to ignore any incoming messages
            continueReceiving = false;

            //wait for child agents to end
            var disposeStarted = DateTime.Now;
            while ((DateTime.Now - disposeStarted).TotalMinutes < maxMinutesWaitForChildAgentCompletion 
                && _agentService.GetChildAgentCount() > 0)
            {
                System.Threading.Thread.Sleep(50);
            }

            //sent stopped message to master if this agent is not the master
            if (AgentId != MpiConsts.MasterAgentId)
            {
                Send(new Message(SessionId, AgentId, MpiConsts.MasterAgentId, SystemMessageTypes.Stopped, null));
            }

            //notify node server this agent is gone
            _nodeServiceProxy.UnRegisterAgent(SessionId, AgentId); 

            //dispose and close other resources
            _incomingMessageWaitHandle.Dispose();
            _nodeServiceFactory.Dispose();
            if (null != _agentServiceHost) _agentServiceHost.Close();
            Log.Close();
        }
    }
}
