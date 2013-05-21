using DuoVia.Net.NamedPipes;
using DuoVia.Net.TcpIp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.NetTestCommon
{
    public interface INetTester
    {
        Guid GetId(string source, double weight, int quantity);
        TestResponse Get(Guid id, string label, double weight, int quantity);
        List<string> GetItems(Guid id);
    }

    [Serializable]
    public struct TestResponse
    {
        public Guid Id { get; set; }
        public string Label { get; set; }
        public int Quantity { get; set; }
    }

    public class NetTcpTesterProxy : TcpClient<INetTester>, INetTester
    {
        public NetTcpTesterProxy(IPEndPoint endpoint) : base(endpoint)
        {
        }

        public Guid GetId(string source, double weight, int quantity)
        {
            return Proxy.GetId(source, weight, quantity);
        }

        public TestResponse Get(Guid id, string label, double weight, int quantity)
        {
            return Proxy.Get(id, label, weight, quantity);
        }

        public List<string> GetItems(Guid id)
        {
            return Proxy.GetItems(id);
        }
    }

    public class NetNpTesterProxy : NpClient<INetTester>, INetTester
    {
        public NetNpTesterProxy(NpEndPoint npAddress) : base(npAddress)
        {
        }

        public Guid GetId(string source, double weight, int quantity)
        {
            return Proxy.GetId(source, weight, quantity);
        }

        public TestResponse Get(Guid id, string label, double weight, int quantity)
        {
            return Proxy.Get(id, label, weight, quantity);
        }

        public List<string> GetItems(Guid id)
        {
            return Proxy.GetItems(id);
        }
    }
}
