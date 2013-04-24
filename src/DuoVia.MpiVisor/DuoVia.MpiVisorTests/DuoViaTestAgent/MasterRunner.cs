using DuoVia.MpiVisor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoViaTestAgent
{
    internal static class MasterRunner
    {
        private static ushort numberOfAgentsToSpawn = 1;

        //use to stop message processing loop
        private static bool continueProcessing = true;

        //additional means of determining when to stop processing loop
        private static ushort spawnedAgentsThatHaveStoppedRunning = 0;

        public static void Run(string[] args)
        {
            //spawn worker agents, send messages and orchestrate work
            Agent.Current.SpawnAgents(numberOfAgentsToSpawn, args);
            Message msg;
            do
            {
                msg = Agent.Current.ReceiveAnyMessage();
                switch (msg.MessageType)
                {
                    //handle content types > -1 which are application specific
                    //case 0-~:
                        //handle messages from master or other agents here
                        //break;
                    case 2:
                        //handle messages from master or other agents here
                        Log.Info("AgentId {0} sent message type 2 with {1}", msg.FromId, msg.Content);

                        //this test/demo just sends the message back to the sender
                        Agent.Current.Send(
                            toAgentId: msg.FromId,
                            messageType: SystemMessageTypes.Shutdown,
                            content: null);
                        break;

                    //handle internal messages and unknown
                    case SystemMessageTypes.Started:
                        Log.Info("AgentId {0} reports being started.", msg.FromId);
                        //send demo/test content message
                        Agent.Current.Send(
                            toAgentId: msg.FromId,
                            messageType: 1,
                            content: "hello from 1");
                        break;
                    case SystemMessageTypes.Stopped:
                        Log.Info("AgentId {0} reports being stopped.", msg.FromId);
                        spawnedAgentsThatHaveStoppedRunning++;
                        break;
                    case SystemMessageTypes.Aborted:
                        Log.Info("AgentId {0} reports being aborted.", msg.FromId);
                        spawnedAgentsThatHaveStoppedRunning++;
                        break;
                    case SystemMessageTypes.Error:
                        Log.Info("AgentId {0} reports an error.", msg.FromId);
                        break;
                    default:
                        Log.Info("AgentId {0} sent {1} with {2}", msg.FromId, msg.MessageType, msg.Content);
                        break;
                }
            }
            while (continueProcessing && spawnedAgentsThatHaveStoppedRunning < numberOfAgentsToSpawn);
            
            //change while logic as desired to keep master running or shut it down and all other agents will as well
            Log.Info("done master");
        }
    }
}
