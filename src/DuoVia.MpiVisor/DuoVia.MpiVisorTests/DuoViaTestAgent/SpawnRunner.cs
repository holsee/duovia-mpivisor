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
            SetAbortFunction();
            Message msg;
            do
            {
                msg = Agent.Current.MessageQueue.ReceiveAnyMessage();
                switch (msg.MessageType)
                {
                    //handle content types > -1 which are application specific
                    case 1:
                        //handle messages from master or other agents here
                        Log.Info("AgentId {0} sent message type 1 with {1}", msg.FromId, msg.Content);

                        //this test/demo just sends the message back to the sender
                        Agent.Current.MessageQueue.Send(
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
                    case SystemMessageTypes.NullMessage:
                        //this means the agent has waited more than the allotted time for a message
                        //or the AbortMessageWaitVisitor function was set and returned true 
                        //and a null message was returned by the ReceiveAnyMessage method
                        //so the developer must decide whether to shut down or continue waiting
                        Log.Info("Visor reports message wait timed out and a null message was returned.");
                        continueProcessing = false;  //a stopped message will be sent to the master
                        break;
                    default:
                        Log.Info("AgentId {0} sent {1} with {2}", msg.FromId, msg.MessageType, msg.Content);
                        break;
                }
            }
            while (continueProcessing);
            Log.Info("work done");
        }

        private static void SetAbortFunction()
        {
            //the count parameter is the number of times the receive message method 
            //has waited for the WaitForMessageAbortTimeMs milliseconds before calling
            //the AbortMessageWaitVisitor function here, so in this demo, the function
            //will be called every 800ms while waiting for a message to be received
            //this allows the developer to inject logic into the decision as to whether
            //the receive message method should return a null message rather than block for
            //the entire timeout period defined in the call to the receive message method

            //so if count = 3, then you have waited 3 * 800ms for a message to arrive

            //warning: an exception thrown in this logic will result in a "false" being returned 
            //         and waiting will continue and the exception will not be raised

            //note: you can set the same logic for master and spawned agent by setting these 
            //      values prior to the if this is a master logic or after and only within
            //      the master or spawned agent code

            //be sure to set these before entering the message wait loop and review
            //the case SystemMessageTypes.NullMessage: comments and code

            Agent.Current.MessageQueue.WaitForMessageAbortTimeMs = 800; //default is 1000
            Agent.Current.MessageQueue.AbortMessageWaitVisitor = (count) =>
            {
                //no logic, just a demo, returning false here which means we 
                //will not abort the receive message message
                return false; 
            };
        }
    }
}
