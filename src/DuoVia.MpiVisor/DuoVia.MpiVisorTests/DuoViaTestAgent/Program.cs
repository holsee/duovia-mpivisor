using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor;

namespace DuoViaTestAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            //connect agent and dispose at end of execution
            //use forceLocal to run in a single process with internal visor
            using (Agent.Connect(forceLocal: false)) 
            {
                //default is File only - spawned agents shuttle logs back to master
                Log.LogType = LogType.Both; 
                if (Agent.Current.IsMaster)
                {
                    try
                    {
                        //keep Main clean with master message loop class
                        MasterRunner.Run(args);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Agent master exception: {0}", e);
                    }
                }
                else
                {
                    try
                    {
                        //keep Main clean with spawned agent message loop class
                        SpawnRunner.Run(args);
                    }
                    catch (Exception e)
                    {
                        Log.Error("spawn agent exception: {0}", e);
                        Agent.Current.Send(new Message(Agent.Current.SessionId, Agent.Current.AgentId,
                            MpiConsts.MasterAgentId, SystemMessageTypes.Aborted, e.ToString()));
                    }
                }
            }
        } //standard ending - will force service to kill spawned agents  
    }
}
