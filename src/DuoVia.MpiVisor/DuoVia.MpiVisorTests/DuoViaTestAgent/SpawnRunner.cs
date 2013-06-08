using DuoVia.MpiVisor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoViaTestAgent
{
    internal static class SpawnRunner
    {
        private static bool continueProcessing = true;

        public static void Run(string[] args)
        {
            Message msg;
            do
            {
                msg = Agent.Current.ReceiveAnyMessage();
                switch (msg.MessageType)
                {
                    //handle content types > -1 which are application specific
                    case 1:
                        //handle messages from master or other agents here
                        Log.Info("AgentId {0} sent message type 1 with {1}", msg.FromId, msg.Content);

                        //this test/demo just sends the message back to the sender
                        Agent.Current.Send(
                            toAgentId: msg.FromId, 
                            messageType: 2, 
                            content: msg.Content.ToString() + " received");
                        break;

                    //handle internal messages and unknown
                    case SystemMessageTypes.Shutdown:
                        Log.Info("AgentId {0} sent shut down message", msg.FromId);
                        continueProcessing = false;
                        break;
                    case SystemMessageTypes.DeliveryFailure:
                        //this means master is no longer responding, so shut this agent down
                        //we only send messages to master here - had we sent to another agent,
                        //we would need to check to see if the msg.FromId is 0 (master)
                        //the msg.Content contains the orginal Message object sent
                        Log.Info("Visor reports message delivery failure.", msg.FromId);
                        continueProcessing = false;
                        break;
                    default:
                        Log.Info("AgentId {0} sent {1} with {2}", msg.FromId, msg.MessageType, msg.Content);
                        break;
                }
            }
            while (continueProcessing);
            Log.Info("work done");
        }
    }
}
