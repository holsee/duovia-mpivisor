using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;

namespace DuoVia.Net.TcpIp
{
    public sealed class TcpProxy
    {
        public static TInterface CreateProxy<TInterface>(IPEndPoint endpoint) where TInterface : class
        {
            return ProxyFactory.CreateProxy<TInterface>(typeof(TcpChannel), typeof(IPEndPoint), endpoint);
        }
    }
}
