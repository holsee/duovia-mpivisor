using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DuoVia.MpiVisor.Services;

namespace DuoVia.MpiVisor
{
    public interface IWorkerFactory
    {
        void SpawnWorkerAgents(ushort count, string[] args);
        void SpawnCountOfWorkerAgentsPerNode(string[] args, int count);
        void SpawnOneWorkerAgentAsFactorOfLogicalProcessorCount(string[] args, double factor);
        void SpawnOneWorkerAgentPerLogicalProcessor(string[] args);
        void SpawnOneWorkerAgentPerLogicalProcessorLessCount(string[] args, int count);
        void SpawnOneWorkerAgentPerLogicalProcessorLessOne(string[] args);
        void SpawnOneWorkerAgentPerNode(string[] args);
    }

    public sealed class WorkerFactory : IWorkerFactory
    {
        bool _isInternalNodeServer = false;
        INodeService _nodeServiceProxy = null;
        SessionInfo _session;

        public WorkerFactory(SessionInfo session, INodeService nodeServiceProxy, bool isInternalNodeServer)
        {
            _session = session;
            _nodeServiceProxy = nodeServiceProxy;
            _isInternalNodeServer = isInternalNodeServer;
        }

        /// <summary>
        /// Spawn worker agents from master agent. Only master agent can spawn worker agents.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="args"></param>
        public void SpawnWorkerAgents(ushort count, string[] args)
        {
            SpawnInternal(count, args, SpawnStrategy.None, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneWorkerAgentPerNode(string[] args)
        {
            SpawnInternal(1, args, SpawnStrategy.OneAgentPerNode, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per logical processor on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneWorkerAgentPerLogicalProcessor(string[] args)
        {
            SpawnInternal(1, args, SpawnStrategy.OneAgentPerLogicalProcessor, 0.0);
        }

        /// <summary>
        /// Spawn one worker agent per logical processor, less one, on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        public void SpawnOneWorkerAgentPerLogicalProcessorLessOne(string[] args)
        {
            SpawnInternal(1, args, SpawnStrategy.OneAgentPerLogicalProcessorLessOne, 0.0);
        }
        /// <summary>
        /// Spawn one worker agent per logical processor, less one, on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="count">agents = LogicalProcessCount - count</param>
        public void SpawnOneWorkerAgentPerLogicalProcessorLessCount(string[] args, int count)
        {
            SpawnInternal(1, args, SpawnStrategy.OneAgentPerLogicalProcessorLessCount, Convert.ToDouble(count));
        }
        /// <summary>
        /// Spawn worker agents by factor multiplied by number of logical processors on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="factor">agents = LogicalProcessCount * factor</param>
        public void SpawnOneWorkerAgentAsFactorOfLogicalProcessorCount(string[] args, double factor)
        {
            SpawnInternal(1, args, SpawnStrategy.OneAgentAsFactorOfLogicalProcessorCount, factor);
        }
        /// <summary>
        /// Spawn count of worker agents on each cluster node including node executing master agent.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="count">agents = count</param>
        public void SpawnCountOfWorkerAgentsPerNode(string[] args, int count)
        {
            SpawnInternal(1, args, SpawnStrategy.CountOfAgentsPerNode, Convert.ToDouble(count));
        }

        private void SpawnInternal(ushort count, string[] args, int strategy, double factor)
        {
            if (count == MpiConsts.MasterAgentId || count == MpiConsts.BroadcastAgentId)
            {
                throw new ArgumentException("Count must be between 1 and 65,534", "count");
            }
            var package = _isInternalNodeServer ? new byte[0] : ZipUtils.PackageAgent();
            var entryAssembly = Assembly.GetEntryAssembly();
            var agentExecutableName = Path.GetFileName(entryAssembly.Location);
            if (strategy > 0)
                _nodeServiceProxy.SpawnStrategic(_session, count, agentExecutableName, package, args, strategy, factor);
            else
                _nodeServiceProxy.Spawn(_session, count, agentExecutableName, package, args);
        }
    }
}
