using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor.Services;

namespace DuoVia.MpiVisor
{
    public static class Visor
    {
        /// <summary>
        /// Create Agent context and connect to node service. Must manually choose whether to run local or distributed.
        /// Allows injection of Agent dependencies. Set any to null and the default will be used.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="runInSingleLocalProcess"></param>
        /// <param name="nodeServiceFactory"></param>
        /// <param name="agentService"></param>
        /// <param name="messageQueue"></param>
        /// <param name="workerFactory"></param>
        /// <returns></returns>
        public static Agent Connect(string[] args, bool runInSingleLocalProcess, 
            INodeServiceFactory nodeServiceFactory, IAgentService agentService, IMessageQueue messageQueue, IWorkerFactory workerFactory)
        {
            return Agent.Create(args, runInSingleLocalProcess, nodeServiceFactory, agentService, messageQueue, workerFactory);
        }

        /// <summary>
        /// Create Agent context and connect to node service to run local only (no distribution).
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Agent ConnectLocal(string[] args)
        {
            return Connect(args, true, null, null, null, null);
        }

        /// <summary>
        /// Create Agent context and connect to node service to run local only (no distribution).
        /// Allows injection of Agent dependencies. Set any to null and the default will be used.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="nodeServiceFactory"></param>
        /// <param name="agentService"></param>
        /// <param name="messageQueue"></param>
        /// <param name="workerFactory"></param>
        /// <returns></returns>
        public static Agent ConnectLocal(string[] args, 
            INodeServiceFactory nodeServiceFactory, IAgentService agentService, IMessageQueue messageQueue, IWorkerFactory workerFactory)
        {
            return Connect(args, true, nodeServiceFactory, agentService, messageQueue, workerFactory);
        }

        /// <summary>
        /// Create Agent context and connect to node service distributed on node servers if a node exists on same machine as executable.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Agent ConnectDistributed(string[] args)
        {
            return Connect(args, false, null, null, null, null);
        }

        /// <summary>
        /// Create Agent context and connect to node service distributed on node servers if a node exists on same machine as executable.
        /// Allows injection of Agent dependencies. Set any to null and the default will be used.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="nodeServiceFactory"></param>
        /// <param name="agentService"></param>
        /// <param name="messageQueue"></param>
        /// <param name="workerFactory"></param>
        /// <returns></returns>
        public static Agent ConnectDistributed(string[] args, 
            INodeServiceFactory nodeServiceFactory, IAgentService agentService, IMessageQueue messageQueue, IWorkerFactory workerFactory)
        {
            return Connect(args, false, nodeServiceFactory, agentService, messageQueue, workerFactory);
        }
    }
}
