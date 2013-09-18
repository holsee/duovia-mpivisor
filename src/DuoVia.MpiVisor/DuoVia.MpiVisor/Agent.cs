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
    public class Agent : IDisposable
    {
        //singleton instance
        private static Agent _current = null;

        //initialization values required to create Agent dependencies
        private readonly DateTime _startedAt = DateTime.Now;
        private readonly string _arguments = null;
        private readonly bool _runInSingleLocalProcess = false;

        //node service factory and client proxy - allows agent to talk to node service
        private INodeServiceFactory _nodeServiceFactory = null;
        private INodeService _nodeServiceProxy = null;

        //agent service host and implementation - allows node service to talk to agent
        private IAgentService _agentService = null;
        private NpHost _agentServiceHost = null;

        //dependency to create or get session and set agent id
        private ISessionFactory _sessionFactory = null;

        //agent spawning factory dependency
        private IWorkerFactory _workerFactory = null;

        //message queue dependency
        IMessageQueue _messageQueue = null;

        //determines how long a primary agent (the process) will wait for a child agent (app domain)
        //to complete and dispose before completing the dispose of the primary agent
        private const int maxMinutesWaitForChildAgentCompletion = 20;

        //singleton - cannot create instance publicly - this is called by the Connect method
        private Agent(string arguments, bool runInSingleLocalProcess, 
            INodeServiceFactory nodeServiceFactory, IAgentService agentService, IMessageQueue messageQueue, IWorkerFactory workerFactory)
        {
            _arguments = arguments;
            _runInSingleLocalProcess = runInSingleLocalProcess;
            
            //optional injected dependencies - if null, InitializeAgentState will create default from implementation
            _nodeServiceFactory = nodeServiceFactory;
            _agentService = agentService;
            _messageQueue = messageQueue;
            _workerFactory = workerFactory;
            
            //now we need to set up the agent's state to determine agentId, Session info, and create the message queue.
            InitializeAgentState();
        }

        private void InitializeAgentState()
        {
            // initialize context using app domain data, else create master agent
            _sessionFactory = _sessionFactory ?? new SessionFactory();
            ushort assignedAgentId;
            Session = _sessionFactory.CreateSession(_arguments, out assignedAgentId);
            AgentId = assignedAgentId;

            //create connection to NodeService 
            _nodeServiceFactory = _nodeServiceFactory ?? new NodeServiceFactory(AgentId);
            _nodeServiceProxy = _nodeServiceFactory.CreateConnection(this.Session.SessionId, _runInSingleLocalProcess); 

            //open AgentService host to allow server to talk to this agent
            //pipeName is always agent Name to allow multiple agent instances on same machine
            _agentService = _agentService ?? new AgentService();
            _agentServiceHost = new NpHost(_agentService, this.Name); 
            _agentServiceHost.Open();

            //create message queue if not already injected
            _messageQueue = _messageQueue ?? new MessageQueue(AgentId, Session, _nodeServiceProxy);

            //only create an _agentFactory if this is the master - slaves cannot spawn new agents
            //even if dependency was injected, set to null if not master agent
            _workerFactory = (assignedAgentId == MpiConsts.MasterAgentId) 
                ? _workerFactory ?? new WorkerFactory(Session, _nodeServiceProxy, _nodeServiceFactory.IsInternalServer) 
                : null;
        }

        /// <summary>
        /// Internal. Used only by Visor class.
        /// Call to create and connect current agent context. 
        /// Required to construct singleton Current.
        /// </summary>
        /// <param name="runInSingleLocalProcess"></param>
        /// <returns></returns>
        internal static Agent Create(string[] args, bool runInSingleLocalProcess,
            INodeServiceFactory nodeServiceFactory, IAgentService agentService, IMessageQueue messageQueue, IWorkerFactory workerFactory)
        {
            if (null == _current) //only connect once
            {
                string arguments = (null == args) ? null : string.Join(" ", args);
                _current = new Agent(arguments, runInSingleLocalProcess, nodeServiceFactory, agentService, messageQueue, workerFactory);
                if (_current.IsMaster)
                {
                    _current._nodeServiceProxy.RegisterMasterAgent(_current.Session);
                }
                else
                {
                    //spawned agents send started message back to master agent
                    _current.MessageQueue.Send(new Message(_current.Session.SessionId,
                        _current.AgentId, MpiConsts.MasterAgentId, SystemMessageTypes.Started, null));
                }
            }
            return _current;
        }

        /// <summary>
        /// The current agent context used to interact with InternalVisor or ServerVisor.
        /// </summary>
        public static Agent Current { get { return _current; } }

        /// <summary>
        /// The unique agent id.
        /// </summary>
        public ushort AgentId { get; set; }

        /// <summary>
        /// Complete information about this session.
        /// </summary>
        public SessionInfo Session { get; private set; }

        /// <summary>
        /// Used to spawn new worker agents. Null if not master agent.
        /// </summary>
        public IWorkerFactory WorkerFactory { get { return _workerFactory; } }

        /// <summary>
        /// Used to send and receive messages to and from other agents.
        /// </summary>
        public IMessageQueue MessageQueue { get { return _messageQueue; } }

        /// <summary>
        /// Simple way to determine if the agent id is 0.
        /// </summary>
        public bool IsMaster { get { return (AgentId == 0); } }

        /// <summary>
        /// The name of this agent instance. Combines agent id and session id.
        /// </summary>
        public string Name { get { return string.Format("{0}-{1}", AgentId, (Session != null) ? Session.SessionId : Guid.Empty); } }


        /// <summary>
        /// Get the list of agent ids that are currently running.
        /// </summary>
        /// <returns></returns>
        public ushort[] GetRunningAgents()
        {
            return _nodeServiceProxy.GetRunningAgents(this.Session.SessionId);
        }

        /// <summary>
        /// Terminates processing on all spawned agents. Can only be called by Master agent.
        /// </summary>
        public void KillSession()
        {
            if (AgentId > 0) return;
            _nodeServiceProxy.KillSession(Session.SessionId);
        }

        #region IDisposable members

        private bool _disposed = false;

        public void Dispose()
        {
            //MS recommended dispose pattern - prevents GC from disposing again
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources and send proper message. 
        /// Spawned agent process will wait for child agents before cleaning up.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true; //prevent second call to Dispose
                if (disposing)
                {
                    try
                    {
                        //tell message receiving task to ignore any incoming messages
                        _messageQueue.AllowMessageEnqueing = false;

                        //wait for child agents to end - needed when agents are spawned as app domain from remote primary process agent
                        var disposeStarted = DateTime.Now;
                        while ((DateTime.Now - disposeStarted).TotalMinutes < maxMinutesWaitForChildAgentCompletion
                            && _agentService.GetChildAgentCount() > 0)
                        {
                            System.Threading.Thread.Sleep(50);
                        }

                        //sent stopped message to master if this agent is not the master
                        if (AgentId != MpiConsts.MasterAgentId)
                        {
                            //send stopped message to master
                            _messageQueue.Send(new Message(Session.SessionId, AgentId, MpiConsts.MasterAgentId, SystemMessageTypes.Stopped, null));

                            //notify node server this agent is no longer available
                            _nodeServiceProxy.UnRegisterAgent(Session.SessionId, AgentId);
                        }
                        else
                        {
                            //is master, so shut it all down and dispose of processes, etc.
                            this.KillSession();
                        }

                        //dispose and close other resources
                        _messageQueue.Dispose();
                        _nodeServiceFactory.Dispose();
                        if (null != _agentServiceHost) _agentServiceHost.Dispose();

                    }
                    catch(Exception e)
                    {
                        Log.Error("dispose error {0}", e);
                    }
                    finally
                    {
                        Log.Close();
                    }
                }
            }
        }

        #endregion

    }
}
