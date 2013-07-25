using DuoVia.MpiVisor.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace mpiv
{
    class Program
    {
        static void Main(string[] args)
        {
            //TODO - expand to additional command and parse and show docs
            if (args.Length > 0)
            {
                var command = args[0].ToLower();
                var arguments = ParseArgs(args);
                switch (command)
                {
                    case "start-job":
                        StartJob(arguments);
                        break;
                    case "kill-session":
                        KillSession(arguments);
                        break;
                    default:
                        //get-info
                        GetInfo(arguments);
                        break;
                }
            }
        }

        //args: Start-Job -f "c:\my app\hello.exe" -a "3542 125 clean" -n 192.168.20.102:8089
        static void StartJob(Dictionary<string, string> arguments)
        {
            string exePath = arguments.ContainsKey("f") ? arguments["f"] : null;
            string exeArgs = arguments.ContainsKey("a") ? arguments["a"] : null;
            string result = string.Empty;
            using (var proxy = new ManagementServiceProxy(GetEndpoint(arguments)))
            {
                result = proxy.Run(exePath, exeArgs, null);
            }
            Console.WriteLine(result);
        }

        //args: Kill-Session -s "72029ddb-638f-4226-99ce-9d13ed63e4da" -n 192.168.20.102:8089
        static void KillSession(Dictionary<string, string> arguments)
        {
            var s = arguments.ContainsKey("s") ? arguments["s"] : null;
            Guid session;
            if (Guid.TryParse(s, out session))
            {
                using (var proxy = new ManagementServiceProxy(GetEndpoint(arguments)))
                {
                    proxy.KillSession(session);
                }
            }
            Console.WriteLine("Kill message sent");
        }

        //args: Get-Info -n 192.168.20.102:8089
        static void GetInfo(Dictionary<string, string> arguments)
        {
            ManagementInfo info = null;
            using (var proxy = new ManagementServiceProxy(GetEndpoint(arguments)))
            {
                info = proxy.GetInfo();
            }
            if (null != info)
            {
                Console.WriteLine("Nodes:");
                Console.WriteLine("Name, Processors, IsActive, EndPoint");
                foreach (var node in info.Nodes)
                {
                    Console.WriteLine(":{0}, :{1}, :{2}, :{3}", node.MachineName, node.ProcessorCount, node.IsActive, node.EndPoint);
                }
                Console.WriteLine("");
                Console.WriteLine("Sessions:");
                Console.WriteLine("Id, Name, Args, Created");
                foreach (var session in info.Sessions)
                {
                    Console.WriteLine("{0}{1}{2}{3}", session.SessionId, session.ProcessName, session.Arguments, session.CreatedUtc.ToString("yyyyMMdd-hh:mm:ss.fff"));
                }
            }
        }

        static IPEndPoint GetEndpoint(Dictionary<string, string> arguments)
        {
            if (arguments.ContainsKey("n"))
            {
                //TODO - make smarter and allow name and then get IP for name??
                var vals = arguments["n"].Split(':');
                if (vals.Length == 2)
                {
                    return new IPEndPoint(IPAddress.Parse(vals[0]), Convert.ToInt32(vals[1]));
                }
            }
            return null;
        }

        static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>();
            //skip 0 - that one is the command
            if (args.Length > 2 && (args.Length - 1) % 2 == 0) //even number of args not counting 0
            {
                //TODO - make this more robust
                for (int i = 1; i < args.Length; i += 2) d.Add(args[i].TrimStart('-').ToLower().Substring(0, 1), args[i + 1]);
            }
            return d;
        }
    }
}
