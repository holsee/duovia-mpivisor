using DuoVia.MpiVisor.Services;
using DuoVia.Net.NamedPipes;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuoVia.MpiVisor.Server
{
    internal sealed class ServerVisor : IDisposable
    {
        //singleton instance
        private static readonly ServerVisor _current = new ServerVisor();

        private bool _continueProcessing = true;
        private readonly IPEndPoint _selfEndpoint;
        private readonly IPEndPoint _masterEndpoint;
        private readonly IPEndPoint _backupEndpoint;
        private readonly string _appsRootDir;
        private readonly string _packagesDir;

        private List<ClusterServerInfo> _runningClusterServers = new List<ClusterServerInfo>();
        private Dictionary<Guid, AgentPortfolio> _agentPortfolios = new Dictionary<Guid,AgentPortfolio>();

        private ManualResetEvent _outgoingMessageWaitHandle = new ManualResetEvent(false);
        private LinkedList<Message> _outgoingMessageBuffer = new LinkedList<Message>();
        private Thread _sendMessagesThread = null;

        private ManualResetEvent _spawningWaitHandle = new ManualResetEvent(false);
        private LinkedList<SpawnRequest> _spawnRequestBuffer = new LinkedList<SpawnRequest>();
        private Thread _spawningThread = null;

        public IPEndPoint EndPoint { get { return _selfEndpoint; } }

        private ServerVisor()
        {
            //configure self, master and backup endpoints
            var selfConfig = ConfigurationManager.AppSettings["ClusterNodeAddress"];
            var masterConfig = ConfigurationManager.AppSettings["MasterClusterNodeAddress"];
            var backupConfig = ConfigurationManager.AppSettings["MasterBackupClusterNodeAddress"];

            _selfEndpoint = GetEndPointFromConfig(selfConfig);
            _masterEndpoint = GetEndPointFromConfig(masterConfig);
            _backupEndpoint = GetEndPointFromConfig(backupConfig);
            
            //directories
            _packagesDir = ConfigurationManager.AppSettings["PackagesDirectory"];
            if (string.IsNullOrWhiteSpace(_packagesDir))
                _packagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
            Directory.CreateDirectory(_packagesDir);

            _appsRootDir = ConfigurationManager.AppSettings["AppsDirectory"];
            if (string.IsNullOrWhiteSpace(_appsRootDir))
                _appsRootDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps");
            Directory.CreateDirectory(_appsRootDir);

            //start SendMessages thread
            _sendMessagesThread = new Thread(SendMessages);
            _sendMessagesThread.IsBackground = true;
            _sendMessagesThread.Start();

            _spawningThread = new Thread(SpawnAgents);
            _spawningThread.IsBackground = true;
            _spawningThread.Start();
        }

        public static ServerVisor Current { get { return _current; } }

        public void RegisterInstance()
        {
            lock (_runningClusterServers)
            {
                _runningClusterServers.Add(new ClusterServerInfo 
                    { 
                        EndPoint = _selfEndpoint, 
                        ProcessorCount = (ushort)Environment.ProcessorCount 
                    });

                //if master and spawned agent are same as this node, do nothing more
                if (_selfEndpoint == _masterEndpoint && _selfEndpoint == _backupEndpoint) return; //bail - nothing more to do

                bool tryBackup = false;
                if (!IPEndPoint.Equals(_selfEndpoint, _masterEndpoint))
                {
                    try
                    {
                        using (var proxy = new ClusterServiceProxy(_masterEndpoint))
                        {
                            var nodes = proxy.GetRegisteredNodes();
                            foreach (var node in nodes)
                            {
                                if (!_runningClusterServers.Contains(node)) _runningClusterServers.Add(node);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("error on master proxy: {0}", e);
                        tryBackup = true;
                    }
                }
                if (tryBackup && !IPEndPoint.Equals(_selfEndpoint, _backupEndpoint))
                {
                    try
                    {
                        using (var proxy = new ClusterServiceProxy(_backupEndpoint))
                        {
                            var nodes = proxy.GetRegisteredNodes();
                            foreach (var node in nodes)
                            {
                                if (!_runningClusterServers.Contains(node)) _runningClusterServers.Add(node);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("error on backup proxy: {0}", e);
                    }
                }

                foreach (var node in _runningClusterServers)
                {
                    try
                    {
                        if (!IPEndPoint.Equals(node.EndPoint, _selfEndpoint)) //do not send to self
                        {
                            using (var proxy = new ClusterServiceProxy(node.EndPoint))
                            {
                                proxy.RegisterClusterNode(new ClusterServerInfo { EndPoint = _selfEndpoint, ProcessorCount = (ushort)Environment.ProcessorCount });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("register master agent: {0}", e);
                    }
                }
            }
        }

        public ushort[] GetRunningAgents(Guid sessionId)
        {
            lock (_agentPortfolios)
            {
                if (_agentPortfolios.ContainsKey(sessionId))
                {
                    var portfolio = _agentPortfolios[sessionId];
                    var list = (from n in portfolio.Agents select n.Key).ToArray();
                    return list;
                }
            }
            return new ushort[0];
        }

        private IPEndPoint GetEndPointFromConfig(string config)
        {
            var parts = config.Split(',');
            return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        }

        private void SendMessages(object state)
        {
            while (_continueProcessing)
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
                    lock (_agentPortfolios)
                    {
                        if (!_agentPortfolios.ContainsKey(message.SessionId)) continue; //skip processing 
                        var agentPortfolio = _agentPortfolios[message.SessionId];
                        if (message.ToId == MpiConsts.BroadcastAgentId)
                        {
                            SendBroadcastMessageInternal(message, agentPortfolio);
                        }
                        else
                        {
                            SendMessageInternal(message, agentPortfolio);
                        }
                    }
                }
            }
        }

        private void SendMessageInternal(Message message, AgentPortfolio agentPortfolio)
        {
            //send to specific agent
            var toAgentName = GetAgentName(message.ToId, message.SessionId);
            if (agentPortfolio.LocalAgentIds.Contains(message.ToId))
            {
                //on same node, so no relay required
                try
                {
                    using (var agentProxy = new AgentServiceProxy(new NpEndPoint(toAgentName)))
                    {
                        agentProxy.Send(message);
                    }
                }
                catch (Exception e)
                {
                    SendFailedDeliveryMessage(message);
                    Log.Error("error sending message local: {0}", e);
                }
            }
            else
            {
                //relay this message to remote node
                try
                {
                    var agentEndpoint = (from n in agentPortfolio.Agents
                                         where n.Key == message.ToId
                                         select n.Value).FirstOrDefault();
                    if (null != agentEndpoint)
                    {
                        using (var clusterProxy = new ClusterServiceProxy(agentEndpoint.NodeEndPoint))
                        {
                            clusterProxy.RelayMessage(message);
                        }
                    }
                }
                catch (Exception e)
                {
                    SendFailedDeliveryMessage(message);
                    Log.Error("error relaying message: {0}", e);
                }
            }
        }

        private void SendBroadcastMessageInternal(Message message, AgentPortfolio agentPortfolio)
        {
            //send to all except sender - if message is from an agent on another node this is a relayed message
            //so deliver to local agents only, else send to all nodes
            if (agentPortfolio.LocalAgentIds.Contains(message.FromId))
            {
                lock (_runningClusterServers)
                {
                    //has not been relayed - send to all nodes except this one
                    foreach (var clusterServer in _runningClusterServers)
                    {
                        if (!IPEndPoint.Equals(clusterServer.EndPoint, _selfEndpoint))
                        {
                            try
                            {
                                using (var clusterProxy = new ClusterServiceProxy(clusterServer.EndPoint))
                                {
                                    clusterProxy.RelayMessage(message);
                                }
                            }
                            catch (Exception e)
                            {
                                SendFailedDeliveryMessage(message); 
                                Log.Error("relay message error: {0}", e);
                            }
                        }
                    }
                }
                // now send to all local agents except the agent it was from
                foreach (var agentId in agentPortfolio.LocalAgentIds)
                {
                    if (agentId == message.FromId) continue; //do not sent to self
                    try
                    {
                        var agentName = GetAgentName(agentId, message.SessionId);
                        using (var agentProxy = new AgentServiceProxy(new NpEndPoint(agentName)))
                        {
                            agentProxy.Send(message);
                        }
                    }
                    catch (Exception e)
                    {
                        SendFailedDeliveryMessage(message);
                        Log.Error("local message send error: {0}", e);
                    }
                }
            }
        }

        private void SendFailedDeliveryMessage(Message originalMessage)
        {
            if (originalMessage.MessageType != SystemMessageTypes.DeliveryFailure)
            {
                //reverse direction of original message - from is to and to is from
                EnqueueMessage(new Message(originalMessage.SessionId, originalMessage.ToId, originalMessage.FromId, SystemMessageTypes.DeliveryFailure, originalMessage));
            }
        }

        private void SpawnAgents(object state)
        {
            while (_continueProcessing)
            {
                _spawningWaitHandle.WaitOne();
                SpawnRequest request = null;
                lock (_spawnRequestBuffer)
                {
                    LinkedListNode<SpawnRequest> firstRequest = _spawnRequestBuffer.First;
                    if (firstRequest != null)
                    {
                        request = firstRequest.Value;
                        _spawnRequestBuffer.RemoveFirst();
                    }
                    else
                    {
                        //set to nonsignaled and block on WaitOne again
                        _spawningWaitHandle.Reset();
                    }
                }
                if (null != request)
                {
                    try
                    {
                        if (request.IsVisorDirective)
                        {
                            //spawn local instance
                            ushort agentId = (ushort)(request.Offset + request.Count);
                            Guid sessionId = request.SessionId;
                            SpawnAgent(request, agentId, sessionId);
                            BroadcastSpawnRegisterAgentMessages(agentId, sessionId);
                        }
                        else
                        {
                            BroadcastDirectedSpawnRequests(request);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("spawn agents error: {0}", e);
                    }
                }
            }
        }

        private void SpawnAgent(SpawnRequest request, ushort agentId, Guid sessionId)
        {
            lock (_agentPortfolios)
            {
                if (!_agentPortfolios.ContainsKey(sessionId))
                {
                    _agentPortfolios.Add(sessionId, new AgentPortfolio(sessionId));
                }
                var agentPortfolio = _agentPortfolios[sessionId];
                //create new process when no instance occurs locally
                if (null != request.Package && null == agentPortfolio.LocalProcessAgentName)
                {
                    SpawnAgentProcess(request, agentId, sessionId, agentPortfolio);
                }
                else
                {
                    SpawnAgentInExistingAgentProcess(request, agentId, agentPortfolio);
                }
                agentPortfolio.LocalAgentIds.Add(agentId);
                agentPortfolio.Agents.Add(agentId, new AgentEndPoint(sessionId, agentId, _selfEndpoint));
            }
        }

        private void SpawnAgentProcess(SpawnRequest request, ushort agentId, Guid sessionId, AgentPortfolio agentPortfolio)
        {
            var unpackPath = Path.Combine(_appsRootDir, string.Format("app-{0}", request.SessionId));
            if (!Directory.Exists(unpackPath)) //possible we have already unpacked this one
            {
                //var packageFileName = Path.Combine(_packagesDir, string.Format("p-{0}.zip", request.SessionId));
                Directory.CreateDirectory(unpackPath);
                ZipUtils.UnpackPackage(unpackPath, request.Package);
            }
            //this is our first deploy to this cluster node, so start process
            var basePath = Path.Combine(_appsRootDir, string.Format("app-{0}", request.SessionId));
            var assemblyLocation = Path.Combine(basePath, request.AgentExecutableName);
            var info = (null != request.Args && request.Args.Length > 0)
                ? new ProcessStartInfo(assemblyLocation, string.Join(" ", request.Args))
                : new ProcessStartInfo(assemblyLocation);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.EnvironmentVariables.Add("SessionId", sessionId.ToString());
            info.EnvironmentVariables.Add("AgentId", agentId.ToString());

            var localProcess = Process.Start(info);
            agentPortfolio.LocalProcess = localProcess;
            agentPortfolio.LocalProcessAgentName = GetAgentName(agentId, sessionId);
        }

        private static void SpawnAgentInExistingAgentProcess(SpawnRequest request, ushort agentId, AgentPortfolio agentPortfolio)
        {
            //get agentName of process started for this sesionId
            //call agent service to spawn this additional agent in same process
            if (string.IsNullOrWhiteSpace(agentPortfolio.LocalProcessAgentName)
                || (null == agentPortfolio.LocalProcess
                    && !agentPortfolio.LocalProcessAgentName.StartsWith("0")))
            {
                Log.Error("unable to spawn agent {0} - no process running", agentId);
            }
            else
            {
                using (var agentProxy = new AgentServiceProxy(new NpEndPoint(agentPortfolio.LocalProcessAgentName)))
                {
                    agentProxy.Spawn(agentId, request.AgentExecutableName, request.Args);
                }
            }
        }

        private void BroadcastSpawnRegisterAgentMessages(ushort agentId, Guid sessionId)
        {
            //registered local node in lock, now send message to all
            lock (_runningClusterServers)
            {
                List<Task> tasks = new List<Task>();
                foreach (var node in _runningClusterServers)
                {
                    ClusterServerInfo nodeInstance = node;
                    var task = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint)) //do not send to self
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.RegisterAgent(sessionId, agentId, _selfEndpoint.Address.ToString(), _selfEndpoint.Port);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("register master agent: {0}", e);
                        }
                    }, TaskCreationOptions.LongRunning);
                    tasks.Add(task);
                }
                Task.WaitAll(tasks.ToArray(), 600000); //wait up to ten minutes
            }
        }

        private void BroadcastDirectedSpawnRequests(SpawnRequest request)
        {
            if (request.Strategy > 0)
                BroadcastDirectedSpawnRequestsByStrategy(request);
            else
                BroadcastDirectedSpawnRequestsByAgentCount(request);
        }

        private void BroadcastDirectedSpawnRequestsByStrategy(SpawnRequest request)
        {
            //one directed request per cluster node sent for strategic spawning
            lock (_runningClusterServers)
            {
                ushort offsetIncrement = 0;
                foreach (var node in _runningClusterServers)
                {
                    int agentsPerNode = 1;
                    //only strategy levels 1 and 2 are supported at this time
                    switch(request.Strategy)
                    {
                        case 1:
                            agentsPerNode = 1;
                            break;
                        case 2:
                            agentsPerNode = node.ProcessorCount; 
                            break;
                        case 3:
                            //spawn one agent per cluster visor node cpu/core (logical processors) - 1
                            agentsPerNode = node.ProcessorCount - 1;
                            if (agentsPerNode < 1) agentsPerNode = 1;
                            break;
                        case 4:
                            //spawn one agent per cluster visor node cpu/core (logical processors) - (int)factor
                            if (request.Factor < 0.0) request.Factor = 0.0;
                            if (request.Factor > node.ProcessorCount) request.Factor = node.ProcessorCount - 1.0;
                            agentsPerNode = (ushort)(node.ProcessorCount - (int)request.Factor); 
                            if (agentsPerNode < 1) agentsPerNode = 1;
                            break;
                        case 5:
                            //spawn one agent per cluster visor node cpu/core (logical processors) * factor (percentage)
                            if (request.Factor < 0.1) request.Factor = 0.1;
                            if (request.Factor > node.ProcessorCount * 10.0) request.Factor = node.ProcessorCount * 10.0;
                            agentsPerNode = (ushort)(node.ProcessorCount * request.Factor);
                            if (agentsPerNode < 1) agentsPerNode = 1;
                            break;
                        case 6:
                            //spawn (int)factor agents per cluster visor node cpu/core 
                            if (request.Factor < 0.0) request.Factor = 1.0;
                            if (request.Factor > node.ProcessorCount * 10.0) request.Factor = node.ProcessorCount * 10.0;
                            agentsPerNode = (ushort)((int)request.Factor);
                            if (agentsPerNode < 1) agentsPerNode = 1;
                            break;
                        default:
                            agentsPerNode = node.ProcessorCount; 
                            break;
                    }
                    //send directed spawn request for each agent per node
                    for (int i = 0; i < agentsPerNode; i++)
                    {
                        var directedSpawnRequest = new SpawnRequest
                        {
                            SessionId = request.SessionId,
                            AgentExecutableName = request.AgentExecutableName,
                            Count = 1, //only one agent spawned per directive
                            Offset = (ushort)(request.Offset + offsetIncrement),
                            Package = i == 0 ? request.Package : null,
                            Strategy = request.Strategy,
                            IsVisorDirective = true
                        };
                        //send directed request to cluster node
                        try
                        {
                            using (var proxy = new ClusterServiceProxy(node.EndPoint))
                            {
                                proxy.DirectedSpawnRequest(directedSpawnRequest);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("send spawn request failed: {0}", e);
                        }
                        offsetIncrement++;
                    }
                }
            }
        }

        private void BroadcastDirectedSpawnRequestsByAgentCount(SpawnRequest request)
        {
            //copy package to every registered node and spawn directed requests
            lock (_runningClusterServers)
            {
                //now send Visor directive to spawn locally using simple round robin
                int clusterIndex = 0;

                //if more than one cluster server, begin with one that is next in line from this._selfEndpoint
                if (_runningClusterServers.Count > 1)
                {
                    bool takeNext = false;
                    for (int i = 0; i < _runningClusterServers.Count; i++)
                    {
                        if (takeNext)
                        {
                            clusterIndex = i;
                            break;
                        }
                        if (IPEndPoint.Equals(_selfEndpoint, _runningClusterServers[i].EndPoint))
                        {
                            takeNext = true; //if it is the last, then nothing is set and we start with 0
                        }
                    }
                }

                var hasSentToClusterOnce = new List<int>();
                for (ushort i = 0; i < request.Count; i++)
                {
                    var directedSpawnRequest = new SpawnRequest
                    {
                        SessionId = request.SessionId,
                        AgentExecutableName = request.AgentExecutableName,
                        Count = 1, //only one agent spawned per directive
                        Offset = (ushort)(request.Offset + i),
                        Package = hasSentToClusterOnce.Contains(clusterIndex)
                                    ? null
                                    : request.Package,
                        Strategy = request.Strategy,
                        IsVisorDirective = true
                    };
                    //send this to clusterIndex
                    try
                    {
                        using (var proxy = new ClusterServiceProxy(_runningClusterServers[clusterIndex].EndPoint))
                        {
                            proxy.DirectedSpawnRequest(directedSpawnRequest);
                            if (!hasSentToClusterOnce.Contains(clusterIndex))
                            {
                                //assure we only send package bytes once
                                hasSentToClusterOnce.Add(clusterIndex);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("send spawn request failed: {0}", e);
                    }
                    clusterIndex++;
                    if (clusterIndex == _runningClusterServers.Count) clusterIndex = 0; //roll over
                }
            }
        }

        public void RegisterAgent(Guid sessionId, ushort agentId, string ipAddress, int port)
        {
            lock (_agentPortfolios)
            {
                var agent = new AgentEndPoint(sessionId, agentId, new IPEndPoint(IPAddress.Parse(ipAddress), port));
                if (!_agentPortfolios.ContainsKey(sessionId))
                {
                    _agentPortfolios.Add(sessionId, new AgentPortfolio(sessionId));
                }
                if (!_agentPortfolios[sessionId].Agents.ContainsKey(agentId))
                {
                    _agentPortfolios[sessionId].Agents.Add(agentId, agent);
                }
            }
        }

        public void UnRegisterAgent(Guid sessionId, ushort agentId)
        {
            lock (_agentPortfolios)
            {
                if (_agentPortfolios.ContainsKey(sessionId))
                {
                    var p = _agentPortfolios[sessionId];
                    if (p.LocalAgentIds.Contains(agentId)) p.LocalAgentIds.Remove(agentId);
                    if (p.Agents.ContainsKey(agentId)) p.Agents.Remove(agentId);
                }
            }
        }

        public void KillSession(Guid sessionId)
        {
            //send message to all running cluster servers to kill session
            lock (_runningClusterServers)
            {
                foreach (var node in _runningClusterServers)
                {
                    var nodeInstance = node;
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint)) //do not send to self
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.KillSession(sessionId);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error("register master agent: {0}", e);
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }

            //kill local session agents
            KillSessionLocal(sessionId);
        }

        public void KillSessionLocal(Guid sessionId)
        {
            lock (_agentPortfolios)
            {
                if (!_agentPortfolios.ContainsKey(sessionId)) return; //nothing to do
                var agentPortfolio = _agentPortfolios[sessionId];
                if (null != agentPortfolio.LocalProcess)
                {
                    try
                    {
                        if (!agentPortfolio.LocalProcess.HasExited)
                        {
                            agentPortfolio.LocalProcess.Kill();
                            agentPortfolio.LocalProcess.WaitForExit();
                            agentPortfolio.LocalProcess.Close();
                            agentPortfolio.LocalProcess = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("failed to kill agent process: {0}", e);
                    }
                }

                foreach (var agentId in agentPortfolio.LocalAgentIds)
                {
                    agentPortfolio.Agents.Remove(agentId);
                }
                agentPortfolio.LocalAgentIds.Clear();

                //now delete local app execution folder - cannot delete immediately
                var folder = Path.Combine(_appsRootDir, string.Format("app-{0}", sessionId));
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception e)
                {
                    Log.Error("unable to delete session folder {0} error: {1}", folder, e);
                }
            }
        }

        public void UnRegisterLocalAgent(Guid sessionId, ushort agentId)
        {
            lock (_agentPortfolios)
            {
                if (_agentPortfolios.ContainsKey(sessionId))
                {
                    var p = _agentPortfolios[sessionId];
                    if (p.LocalAgentIds.Contains(agentId)) p.LocalAgentIds.Remove(agentId);
                    if (p.Agents.ContainsKey(agentId)) p.Agents.Remove(agentId);
                }
            }
            BroadcastUnRegisterLocalAgentNotifications(sessionId, agentId);
        }

        private void BroadcastUnRegisterLocalAgentNotifications(Guid sessionId, ushort agentId)
        {
            lock (_runningClusterServers)
            {
                foreach (var node in _runningClusterServers)
                {
                    var nodeInstance = node;
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint)) //do not send to self
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.UnRegisterAgent(sessionId, agentId);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error("register master agent: {0}", e);
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void RegisterMasterAgent(Guid sessionId)
        {
            lock (_agentPortfolios)
            {
                if (!_agentPortfolios.ContainsKey(sessionId))
                {
                    _agentPortfolios.Add(sessionId, new AgentPortfolio(sessionId));
                }
                if (_agentPortfolios.ContainsKey(sessionId))
                {
                    var masterAgent = new AgentEndPoint(sessionId, 0, _selfEndpoint);
                    var p = _agentPortfolios[sessionId];
                    if (!p.LocalAgentIds.Contains(MpiConsts.MasterAgentId)) p.LocalAgentIds.Add(MpiConsts.MasterAgentId);
                    if (!p.Agents.ContainsKey(MpiConsts.MasterAgentId)) p.Agents.Add(MpiConsts.MasterAgentId, masterAgent);
                    p.LocalProcessAgentName = GetAgentName(MpiConsts.MasterAgentId, sessionId);
                }
            }
            SendRegisterMasterAgentNotifications(sessionId);
        }

        private void SendRegisterMasterAgentNotifications(Guid sessionId)
        {
            lock (_runningClusterServers)
            {
                foreach (var node in _runningClusterServers)
                {
                    var nodeInstance = node;
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint)) //do not send to self
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.RegisterAgent(sessionId, 0, _selfEndpoint.Address.ToString(), _selfEndpoint.Port);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error("register master agent: {0}", e);
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void EnqueueMessage(Message message)
        {
            lock (_outgoingMessageBuffer)
            {
                if (_continueProcessing)
                {
                    _outgoingMessageBuffer.AddLast(message);
                    _outgoingMessageWaitHandle.Set();
                }
            }
        }

        public void EnqueueSpawnRequest(SpawnRequest request)
        {
            lock (_spawnRequestBuffer)
            {
                if (_continueProcessing)
                {
                    _spawnRequestBuffer.AddLast(request);
                    _spawningWaitHandle.Set();
                }
            }
        }

        public void RegisterClusterNode(ClusterServerInfo info)
        {
            lock (_runningClusterServers)
            {
                if (!_runningClusterServers.Contains(info)) _runningClusterServers.Add(info);
            }
        }

        public void UnRegisterClusterNode(ClusterServerInfo info)
        {
            lock (_runningClusterServers)
            {
                if (_runningClusterServers.Contains(info)) _runningClusterServers.Remove(info);
            }
        }

        public ClusterServerInfo[] GetRegisteredClusterNodes()
        {
            lock (_runningClusterServers)
            {
                return _runningClusterServers.ToArray();
            }
        }

        private string GetAgentName(ushort agentId, Guid sessionId)
        {
            return string.Format("{0}-{1}", agentId, sessionId);
        }

        private void UnRegister()
        {
            //unregister this node with all others
            lock (_runningClusterServers)
            {
                var self = new ClusterServerInfo { EndPoint = _selfEndpoint, ProcessorCount = (ushort)Environment.ProcessorCount };
                if (_runningClusterServers.Contains(self)) _runningClusterServers.Remove(self);
                foreach (var node in _runningClusterServers)
                {
                    var nodeInstance = node;
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint)) //do not send to self
                    {
                        UnRegisterInternal(nodeInstance);
                    }
                }
            }
        }

        private void UnRegisterInternal(ClusterServerInfo node)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var self = new ClusterServerInfo { EndPoint = _selfEndpoint, ProcessorCount = (ushort)Environment.ProcessorCount };
                    using (var proxy = new ClusterServiceProxy(node.EndPoint))
                    {
                        proxy.UnRegisterClusterNode(self);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("register master agent: {0}", e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            _continueProcessing = false;
            UnRegister();
            _outgoingMessageWaitHandle.Close();
            _spawningWaitHandle.Close();
        }
    }
}
