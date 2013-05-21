using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using DuoVia.Net.NamedPipes;
using DuoVia.Net.TcpIp;
using DuoVia.NetTestCommon;

namespace DuoVia.NetTestHost
{
	class Program
	{
		static void Main(string[] args)
		{
			var tester = new NetTester();
			var pipeName = "DuoViaTestHost";
			var nphost = new NpHost(tester, pipeName);
			nphost.Open();

			var ipEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8098);
			var tcphost = new TcpHost(tester, ipEndpoint);
			tcphost.Open();

			Console.WriteLine("Press Enter to stop the dual host test.");
			Console.ReadLine();

			nphost.Close();
			tcphost.Close();

			Console.WriteLine("Press Enter to quit.");
			Console.ReadLine();
		}
	}

	public class NetTester : INetTester
	{
		public Guid GetId(string source, double weight, int quantity)
		{
			return Guid.NewGuid();
		}

		public TestResponse Get(Guid id, string label, double weight, int quantity)
		{
			return new TestResponse { Id = id, Label = "Hello, world.", Quantity = 42 };
		}

		public List<string> GetItems(Guid id)
		{
			var list = new List<string>();
			list.Add("42");
			list.Add(id.ToString());
			list.Add("Test");
			return list;
		}
	}
}
