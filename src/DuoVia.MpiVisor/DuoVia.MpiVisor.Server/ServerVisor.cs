using DuoVia.MpiVisor.Management;
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
        private readonly string _appsRootDir;
        private readonly string _packagesDir;

        private ClusterServerConfig _clusterServerConfig = ClusterServerConfig.Load();
        private Dictionary<Guid, AgentPortfolio> _agentPortfolios = new Dictionary<Guid, AgentPortfolio>();

        private ManualResetEvent _outgoingMessageWaitHandle = new ManualResetEvent(false);
        private Queue<Message> _outgoingMessageBuffer = new Queue<Message>();
        private Thread _sendMessagesThread = null;

        private ManualResetEvent _spawningWaitHandle = new ManualResetEvent(false);
        private Queue<SpawnRequest> _spawnRequestBuffer = new Queue<SpawnRequest>();
        private Thread _spawningThread = null;

        public IPEndPoint EndPoint { get { return _selfEndpoint; } }

        private ServerVisor()
        {
            //configure self, master and backup endpoints
            var selfConfig = ConfigurationManager.AppSettings["ClusterNodeAddress"];
            _selfEndpoint = GetEndPointFromConfig(selfConfig);
            
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
            lock (_clusterServerConfig)
            {
                var self = new ClusterServerInfo
                    {
                        EndPoint = _selfEndpoint,
                        ProcessorCount = (ushort)Environment.ProcessorCount,
                        MachineName = Environment.MachineName,
                        IsActive = true
                    };

                //get registered nodes from other running nodes to compare and reconcile config
                ClusterServerInfo[] registeredNodes = null;
                for (int i = 0; i < _clusterServerConfig.ClusterServers.Count; i++)
                {
                    var node = _clusterServerConfig.ClusterServers[i];
                    if (!node.Equals(self)) //any but self
                    {
                        try
                        {
                            //TODO - use IsActive first and then try inactive nodes?
                            using (var proxy = new ClusterServiceProxy(node.EndPoint))
                            {
                                registeredNodes = proxy.GetRegisteredNodes();
                            }
                            break;
                        }
                        catch (Exception e)
                        {
                            //skip logging - means cannot connect to node - try next one
                            //Log.Error("error on proxy: {0}", e);
                        }
                    }
                }
                if (null != registeredNodes && registeredNodes.Length > 0)
                {
                    //add or update registered nodes
                    foreach (var node in registeredNodes)
                    {
                        //update or add
                        var index = _clusterServerConfig.ClusterServers.IndexOf(node);
                        if (index > -1)
                        {
                            _clusterServerConfig.ClusterServers[index] = node;
                        }
                        else
                        {
                            _clusterServerConfig.ClusterServers.Add(node);
                        }
                    }

                    //update configured nodes that are not in registered node list
                    foreach (var node in _clusterServerConfig.ClusterServers)
                    {
                        //do not change self here
                        if (!node.EndPoint.Equals(_selfEndpoint))
                        {
                            if (!registeredNodes.Contains(node)) node.IsActive = false;
                        }
                    }
                }
                else
                {
                    //no response from any remote nodes, so set all non "self" nodes to IsActive = false
                    foreach (var node in _clusterServerConfig.ClusterServers)
                    {
                        if (!node.EndPoint.Equals(_selfEndpoint)) node.IsActive = false;
                    }
                }

                //update self to active or add this node to config if not already there
                var selfIndex = _clusterServerConfig.ClusterServers.IndexOf(self);
                if (selfIndex > -1)
                {
                    _clusterServerConfig.ClusterServers[selfIndex].IsActive = true;
                }
                else
                {
                    _clusterServerConfig.ClusterServers.Add(self);
                }

                //persist to disk
                _clusterServerConfig.Save();

                //notify all other active nodes
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    try
                    {
                        //do not send to self and send only to active nodes
                        if (!IPEndPoint.Equals(node.EndPoint, _selfEndpoint) && node.IsActive)
                        {
                            using (var proxy = new ClusterServiceProxy(node.EndPoint))
                            {
                                proxy.RegisterClusterNode(new ClusterServerInfo 
                                { 
                                    EndPoint = _selfEndpoint, 
                                    ProcessorCount = (ushort)Environment.ProcessorCount, 
                                    MachineName = Environment.MachineName,
                                    IsActive = true
                                });
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
                    if (_outgoingMessageBuffer.Count > 0)
                    {
                        message = _outgoingMessageBuffer.Dequeue();
                        //attempt to prevent excessive memory footprint
                        if (_outgoingMessageBuffer.Count > 1000) _outgoingMessageBuffer.TrimExcess(); 
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
                    using (var agentProxy = new AgentServiceProxy(new NpEndPoint(toAgentName, 2500)))
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
                lock (_clusterServerConfig)
                {
                    //has not been relayed - send to all nodes except this one
                    foreach (var clusterServer in _clusterServerConfig.ClusterServers)
                    {
                        //do not send to self and send only to active nodes
                        if (!IPEndPoint.Equals(clusterServer.EndPoint, _selfEndpoint) && clusterServer.IsActive)
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
                        using (var agentProxy = new AgentServiceProxy(new NpEndPoint(agentName, 2500)))
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
                    if (_spawnRequestBuffer.Count > 0)
                    {
                        request = _spawnRequestBuffer.Dequeue();
                        //attempt to prevent excessive memory footprint
                        if (_spawnRequestBuffer.Count > 1000) _spawnRequestBuffer.TrimExcess(); 
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
                            SpawnAgent(request, agentId, request.Session.SessionId);
                            BroadcastSpawnRegisterAgentMessages(agentId, request.Session);
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
                    _agentPortfolios.Add(sessionId, new AgentPortfolio(request.Session));
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
                agentPortfolio.Agents.Add(agentId, new AgentEndPoint(request.Session, agentId, _selfEndpoint));
            }
        }

        private void SpawnAgentProcess(SpawnRequest request, ushort agentId, Guid sessionId, AgentPortfolio agentPortfolio)
        {
            var unpackPath = Path.Combine(_appsRootDir, string.Format("app-{0}", request.Session.SessionId));
            if (!Directory.Exists(unpackPath)) //possible we have already unpacked this one
            {
                //var packageFileName = Path.Combine(_packagesDir, string.Format("p-{0}.zip", request.SessionId));
                Directory.CreateDirectory(unpackPath);
                AgentPackager.UnpackPackage(unpackPath, request.Package);
            }
            //this is our first deploy to this cluster node, so start process
            var basePath = Path.Combine(_appsRootDir, string.Format("app-{0}", request.Session.SessionId));
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
            agentPortfolio.LocalProcessAgentId = agentId;
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
                using (var agentProxy = new AgentServiceProxy(new NpEndPoint(agentPortfolio.LocalProcessAgentName, 2500)))
                {
                    agentProxy.Spawn(agentId, request.AgentExecutableName, request.Args);
                }
            }
        }

        private void BroadcastSpawnRegisterAgentMessages(ushort agentId, SessionInfo sessionInfo)
        {
            //registered local node in lock, now send message to all
            lock (_clusterServerConfig)
            {
                List<Task> tasks = new List<Task>();
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    ClusterServerInfo nodeInstance = node;
                    var task = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            //do not send to self and send only to active nodes
                            if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint) && nodeInstance.IsActive)
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.RegisterAgent(sessionInfo, agentId, _selfEndpoint.Address.ToString(), _selfEndpoint.Port);
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
            lock (_clusterServerConfig)
            {
                ushort offsetIncrement = 0;
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    if (!node.IsActive) continue; //skip inactive nodes
                    int agentsPerNode = 1;
                    //strategy levels 1 thru 6 are supported at this time
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
                            Session = request.Session,
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
            lock (_clusterServerConfig)
            {
                //now send Visor directive to spawn locally using simple round robin
                int clusterIndex = 0;

                //if more than one cluster server, begin with one that is next in line from this._selfEndpoint
                var firstNotSelfIsActive = (from n in _clusterServerConfig.ClusterServers
                                            where n.IsActive && !IPEndPoint.Equals(_selfEndpoint, n.EndPoint)
                                            select n).FirstOrDefault();
                if (null == firstNotSelfIsActive) //if all others are inactive, start with self
                {
                    firstNotSelfIsActive = (from n in _clusterServerConfig.ClusterServers
                                            where n.IsActive
                                            select n).FirstOrDefault();
                }
                clusterIndex = (null != firstNotSelfIsActive) ? _clusterServerConfig.ClusterServers.IndexOf(firstNotSelfIsActive) : 0;

                var hasSentToClusterOnce = new List<int>();
                for (ushort i = 0; i < request.Count; i++)
                {
                    var directedSpawnRequest = new SpawnRequest
                    {
                        Session = request.Session,
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
                        using (var proxy = new ClusterServiceProxy(_clusterServerConfig.ClusterServers[clusterIndex].EndPoint))
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
                    //find next index for active node
                    bool hasRolledOnce = false;
                    while (true)
                    {
                        clusterIndex++;
                        if (clusterIndex == _clusterServerConfig.ClusterServers.Count)
                        {
                            if (hasRolledOnce) break; //escape -- all inactive (review)
                            clusterIndex = 0; //roll over
                            hasRolledOnce = true;
                        }

                        //if found an active node, break out of loop
                        if (_clusterServerConfig.ClusterServers[clusterIndex].IsActive) break;
                    }
                }
            }
        }

        public void RegisterAgent(SessionInfo sessionInfo, ushort agentId, string ipAddress, int port)
        {
            lock (_agentPortfolios)
            {
                var agent = new AgentEndPoint(sessionInfo, agentId, new IPEndPoint(IPAddress.Parse(ipAddress), port));
                if (!_agentPortfolios.ContainsKey(sessionInfo.SessionId))
                {
                    _agentPortfolios.Add(sessionInfo.SessionId, new AgentPortfolio(sessionInfo));
                }
                if (!_agentPortfolios[sessionInfo.SessionId].Agents.ContainsKey(agentId))
                {
                    _agentPortfolios[sessionInfo.SessionId].Agents.Add(agentId, agent);
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
            lock (_clusterServerConfig)
            {
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    var nodeInstance = node;
                    //do not send to self and send only to active nodes
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint) && nodeInstance.IsActive)
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
                                Log.Error("proxy UnRegisterAgent: {0}", e);
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void RegisterMasterAgent(SessionInfo sessionInfo)
        {
            lock (_agentPortfolios)
            {
                if (!_agentPortfolios.ContainsKey(sessionInfo.SessionId))
                {
                    _agentPortfolios.Add(sessionInfo.SessionId, new AgentPortfolio(sessionInfo));
                }
                if (_agentPortfolios.ContainsKey(sessionInfo.SessionId))
                {
                    var masterAgent = new AgentEndPoint(sessionInfo, 0, _selfEndpoint);
                    var p = _agentPortfolios[sessionInfo.SessionId];
                    if (!p.LocalAgentIds.Contains(MpiConsts.MasterAgentId)) p.LocalAgentIds.Add(MpiConsts.MasterAgentId);
                    if (!p.Agents.ContainsKey(MpiConsts.MasterAgentId)) p.Agents.Add(MpiConsts.MasterAgentId, masterAgent);
                    p.LocalProcessAgentName = GetAgentName(MpiConsts.MasterAgentId, sessionInfo.SessionId);
                }
            }
            SendRegisterMasterAgentNotifications(sessionInfo);
        }

        private void SendRegisterMasterAgentNotifications(SessionInfo sessionInfo)
        {
            lock (_clusterServerConfig)
            {
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    var nodeInstance = node;
                    //do not send to self and send only to active nodes
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint) && nodeInstance.IsActive)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                using (var proxy = new ClusterServiceProxy(nodeInstance.EndPoint))
                                {
                                    proxy.RegisterAgent(sessionInfo, 0, _selfEndpoint.Address.ToString(), _selfEndpoint.Port);
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

        public void KillSession(Guid sessionId)
        {
            //send message to all running cluster servers to kill session
            lock (_clusterServerConfig)
            {
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    var nodeInstance = node;
                    //do not send to self and send only to active nodes
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint) && nodeInstance.IsActive)
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
                                Log.Error("proxy kill session: {0}", e);
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
                _agentPortfolios.Remove(sessionId);
                if (null != agentPortfolio.LocalProcess)
                {
                    try
                    {
                        try
                        {
                            if (!agentPortfolio.LocalProcess.HasExited)
                            {
                                agentPortfolio.LocalProcess.Kill();
                                agentPortfolio.LocalProcess.WaitForExit();
                            }
                        }
                        finally
                        {
                            agentPortfolio.LocalProcess.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("failed to kill agent process: {0}", e);
                    }
                }

                //now delete non-master local app execution folder
                var folder = Path.Combine(_appsRootDir, string.Format("app-{0}", sessionId));
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
                catch (Exception e)
                {
                    Log.Error("unable to delete session folder {0} error: {1}", folder, e);
                }
            }
        }

        public void EnqueueMessage(Message message)
        {
            lock (_outgoingMessageBuffer)
            {
                if (_continueProcessing)
                {
                    _outgoingMessageBuffer.Enqueue(message);
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
                    _spawnRequestBuffer.Enqueue(request);
                    _spawningWaitHandle.Set();
                }
            }
        }

        public void RegisterClusterNode(ClusterServerInfo info)
        {
            lock (_clusterServerConfig)
            {
                var index = _clusterServerConfig.ClusterServers.IndexOf(info);
                if (index > -1)
                {
                    _clusterServerConfig.ClusterServers[index] = info;
                }
                else
                {
                    _clusterServerConfig.ClusterServers.Add(info);
                }
                _clusterServerConfig.Save(); //persist to disk
            }
        }

        public void UnRegisterClusterNode(ClusterServerInfo info)
        {
            lock (_clusterServerConfig)
            {
                info.IsActive = false; //assure we are setting to inactive
                var index = _clusterServerConfig.ClusterServers.IndexOf(info);
                if (index > -1)
                {
                    _clusterServerConfig.ClusterServers[index] = info;
                }
                else
                {
                    _clusterServerConfig.ClusterServers.Add(info);
                }
                _clusterServerConfig.Save(); //persist to disk
            }
        }

        public ClusterServerInfo[] GetRegisteredClusterNodes()
        {
            lock (_clusterServerConfig)
            {
                return _clusterServerConfig.ClusterServers.ToArray();
            }
        }

        private string GetAgentName(ushort agentId, Guid sessionId)
        {
            return string.Format("{0}-{1}", agentId, sessionId);
        }

        private void UnRegister()
        {
            //unregister this node with all others
            lock (_clusterServerConfig)
            {
                var self = new ClusterServerInfo 
                    { 
                        EndPoint = _selfEndpoint, 
                        ProcessorCount = (ushort)Environment.ProcessorCount, 
                        MachineName = Environment.MachineName,
                        IsActive = false
                    };
                var index = _clusterServerConfig.ClusterServers.IndexOf(self);
                if (index > -1)
                {
                    _clusterServerConfig.ClusterServers[index] = self;
                }
                else
                {
                    _clusterServerConfig.ClusterServers.Add(self);
                }
                _clusterServerConfig.Save(); //persist to disk
                var tasks = new List<Task>();
                foreach (var node in _clusterServerConfig.ClusterServers)
                {
                    var nodeInstance = node;
                    //do not send to self and send only to active nodes
                    if (!IPEndPoint.Equals(nodeInstance.EndPoint, _selfEndpoint) && nodeInstance.IsActive)
                    {
                        tasks.Add(UnRegisterInternal(nodeInstance));
                    }
                }
                if (tasks.Count > 0) Task.WaitAll(tasks.ToArray());
            }
        }

        private Task UnRegisterInternal(ClusterServerInfo node)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    var self = new ClusterServerInfo 
                        { 
                            EndPoint = _selfEndpoint, 
                            ProcessorCount = (ushort)Environment.ProcessorCount, 
                            MachineName = Environment.MachineName,
                            IsActive = false
                        };
                    using (var proxy = new ClusterServiceProxy(node.EndPoint))
                    {
                        proxy.UnRegisterClusterNode(self);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("unregister agent: {0}", e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public ManagementInfo GetManagementInfo()
        {
            //private List<ClusterServerInfo> _runningClusterServers = new List<ClusterServerInfo>();
            //private Dictionary<Guid, AgentPortfolio> _agentPortfolios = new Dictionary<Guid, AgentPortfolio>();
            var result = new ManagementInfo();
            lock (_clusterServerConfig)
            {
                var nodeList = new List<NodeInfo>();
                foreach (var item in _clusterServerConfig.ClusterServers)
                {
                    nodeList.Add(new NodeInfo() { EndPoint = item.EndPoint, MachineName = item.MachineName, ProcessorCount = item.ProcessorCount, IsActive = item.IsActive });
                }
                result.Nodes = nodeList.ToArray();
            }
            lock (_agentPortfolios)
            {
                var sessionList = new List<SessionSummary>();
                foreach (var item in _agentPortfolios)
                {
                    var agentCounts = new Dictionary<IPEndPoint, ushort>();
                    foreach (var agent in item.Value.Agents)
                    {
                        if (!agentCounts.ContainsKey(agent.Value.NodeEndPoint))
                            agentCounts.Add(agent.Value.NodeEndPoint, 1);
                        else
                            agentCounts[agent.Value.NodeEndPoint]++;
                    }
                    sessionList.Add(new SessionSummary(item.Value.Session.SessionId, item.Value.Session.ProcessName, item.Value.Session.Arguments, item.Value.Session.CreatedUtc, agentCounts));
                }
                result.Sessions = sessionList.ToArray();
            }
            return result;
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
