using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;

namespace DuoVia.Net.NamedPipes
{
    public sealed class NpProxy
    {
        public static TInterface CreateProxy<TInterface>(NpEndPoint npAddress) where TInterface : class
        {
            return ProxyFactory.CreateProxy<TInterface>(typeof(NpChannel), typeof(NpEndPoint), npAddress);
        }
    }
}
