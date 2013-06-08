using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor;

namespace FindMyPrimes
{
    class Program
    {
        static void Main(string[] args)
        {
            //results on 6 core i7 with 64GB ram and SSDs

            //NOTE: do not try to run both tests at the same time
            //      remember that spawned agents run this same code

            //DoPrimesTheOldWay(args);
            //results single process, chunks of 5,000
            //TOTALS: 20869 primes between 2 and 500000 in 61.7241465 seconds.

            DoPrimesWithMpiVisor(args);
            //results with 2 spawned agents in chunks of 5,000
            //TOTALS: 20869 primes between 2 and 500000 in 31.2239264 seconds.
            
            //results with 6 spawned agents in chunks of 5,000
            //TOTALS: 20869 primes between 2 and 500000 in 13.2928958 seconds.
            
            //results with 12 spawned agents in chunks of 5,000
            //TOTALS: 20869 primes between 2 and 500000 in 10.3193663 seconds.

            //without debugging with 12 spawned agents in chunks of 5,000
            //TOTALS: 20869 primes between 2 and 500000 in 8.3258632 seconds.

            // 6 times faster - at least
        }

        static void DoPrimesTheOldWay(string[] args)
        {
            //find the prime numbers in a certain range by chunks
            var allSw = System.Diagnostics.Stopwatch.StartNew();
            var allPrimes = new List<long>();
            long from = 2;
            long to = 500000;
            var chunkedRequests = Chunkify(from, to, 5000);

            foreach (var chunk in chunkedRequests)
            {
                var result = CalculatePrimes(chunk);
                allPrimes.AddRange(result.Primes);
                //Console.WriteLine("Found {0} primes between {1} and {2} in {3} seconds.",
                //    result.Primes.Count, result.From, result.To, result.TotalSeconds);
            }

            allSw.Stop();
            Console.WriteLine();
            Console.WriteLine("TOTALS: {0} primes between {1} and {2} in {3} seconds.",
                    allPrimes.Count, from, to, allSw.Elapsed.TotalSeconds);
            Console.ReadLine();
        }

        static void DoPrimesWithMpiVisor(string[] args)
        {
            var continueProcessing = true;
            ushort agentsToSpawn = 12;
            ushort agentsShutdown = 0;
            //use Visor.ConnectDistributed to run distributed across nodes
            using (Visor.ConnectLocal(args))
            {
                if (Agent.Current.IsMaster)
                {
                    Log.LogType = LogType.Both; //assure log to console and file
                    //find the prime numbers in a certain range by chunks
                    var allSw = System.Diagnostics.Stopwatch.StartNew();
                    var allPrimes = new List<long>();
                    long from = 2;
                    long to = 500000;
                    var chunkedRequests = Chunkify(from, to, 5000);
                    var nextChunk = 0;

                    Agent.Current.SpawnAgents(agentsToSpawn, args);
                    Message msg;
                    do
                    {
                        msg = Agent.Current.ReceiveAnyMessage();
                        switch(msg.MessageType)
                        {
                            case SystemMessageTypes.Started:
                                if (nextChunk < chunkedRequests.Count)
                                {
                                    Agent.Current.Send(msg.FromId, 1, chunkedRequests[nextChunk]);
                                    nextChunk++;
                                }
                                else
                                {
                                    Agent.Current.Send(msg.FromId, SystemMessageTypes.Shutdown, null);
                                }
                                break;
                            case 2: //agent finished with chunk
                                var result = (PrimesResult)msg.Content;
                                allPrimes.AddRange(result.Primes);
                                //Log.Info("Found {0} primes between {1} and {2} in {3} seconds.",
                                //    result.Primes.Count, result.From, result.To, result.TotalSeconds);
                                if (nextChunk < chunkedRequests.Count)
                                {
                                    Agent.Current.Send(msg.FromId, 1, chunkedRequests[nextChunk]);
                                    nextChunk++;
                                }
                                else
                                {
                                    Agent.Current.Send(msg.FromId, SystemMessageTypes.Shutdown, null);
                                }
                                break;
                            case SystemMessageTypes.Aborted:
                            case SystemMessageTypes.Stopped:
                                agentsShutdown++;
                                if (agentsShutdown >= agentsToSpawn) continueProcessing = false;
                                break;
                            case SystemMessageTypes.DeliveryFailure:
                                //message sent to spawned agent was not able to be delivered
                                //the msg.Content contains the orginal Message object sent
                                Log.Info("Visor reports message delivery failure.", msg.FromId);
                                break;
                            default:
                                Log.Info("AgentId {0} sent {1} with {2}", msg.FromId, msg.MessageType, msg.Content);
                                break;
                        }
                    }
                    while (continueProcessing);
                    allSw.Stop();
                    Console.WriteLine("TOTALS: {0} primes between {1} and {2} in {3} seconds.",
                        allPrimes.Count, from, to, allSw.Elapsed.TotalSeconds);
                    Console.ReadLine();
                }
                else
                {
                    Message msg;
                    do
                    {
                        msg = Agent.Current.ReceiveAnyMessage();
                        switch (msg.MessageType)
                        {
                            case 1:
                                var request = (PrimesRequest)msg.Content;
                                var response = CalculatePrimes(request);
                                Agent.Current.Send(MpiConsts.MasterAgentId, 2, response);
                                break;
                            case SystemMessageTypes.Shutdown:
                                continueProcessing = false;
                                break;
                            case SystemMessageTypes.DeliveryFailure:
                                //this means master is no longer responding, so shut this agent down
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
                }
            }
        }


        //brute force prime finder - consumes more and more time
        static PrimesResult CalculatePrimes(PrimesRequest request)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var primes = new List<long>();
            for (long i = request.From; i < request.To + 1; i++)
            {
                var isPrime = true;
                for (long j = 2; j < i; j++)
                {
                    if (i % j == 0)
                    {
                        isPrime = false;
                        break;
                    }
                }
                if (isPrime) primes.Add(i);
            }
            sw.Stop();
            return new PrimesResult
            {
                From = request.From,
                To = request.To,
                TotalSeconds = sw.Elapsed.TotalSeconds, 
                Primes = primes
            };
        }

        static List<PrimesRequest> Chunkify(long from, long to, int chunkSize)
        {
            var list = new List<PrimesRequest>();
            for (long i = from; i < to + 1; i += chunkSize)
            {
                var chunkFrom = i;
                var chunkTo = i += chunkSize;
                if (chunkTo > to) chunkTo = to;
                list.Add(new PrimesRequest { From = chunkFrom, To = chunkTo });
            }
            return list;
        }
    }

    [Serializable]
    public class PrimesRequest
    {
        public long To { get; set; }
        public long From { get; set; }
    }

    [Serializable]
    public class PrimesResult
    {
        public long To { get; set; }
        public long From { get; set; }
        public List<long> Primes { get; set; }
        public double TotalSeconds { get; set; }
    }
}
