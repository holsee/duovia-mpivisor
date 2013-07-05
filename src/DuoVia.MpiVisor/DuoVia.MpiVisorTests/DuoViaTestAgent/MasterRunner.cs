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
            Agent.Current.WorkerFactory.SpawnWorkerAgents(numberOfAgentsToSpawn, args);
            Message msg;
            do
            {
                msg = Agent.Current.MessageQueue.ReceiveAnyMessage();
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
                        Agent.Current.MessageQueue.Send(
                            toAgentId: msg.FromId,
                            messageType: SystemMessageTypes.Shutdown,
                            content: null);
                        break;

                    //handle internal messages and unknown
                    case SystemMessageTypes.Started:
                        Log.Info("AgentId {0} reports being started.", msg.FromId);
                        //send demo/test content message
                        Agent.Current.MessageQueue.Send(
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
                    case SystemMessageTypes.DeliveryFailure:
                        //message sent to spawned agent was not able to be delivered
                        //the msg.Content contains the orginal Message object sent
                        Log.Info("Visor reports message delivery failure.", msg.FromId);
                        break;
                    case SystemMessageTypes.NullMessage:
                        //this means the agent has waited more than the allotted time for a message
                        //or the AbortMessageWaitVisitor function was set and returned true 
                        //and a null message was returned by the ReceiveAnyMessage method
                        //so the developer must decide whether to shut down or continue waiting
                        Log.Info("Visor reports message wait timed out and a null message was returned.");
                        continueProcessing = false;
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
