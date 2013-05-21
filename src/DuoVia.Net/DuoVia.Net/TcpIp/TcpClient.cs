using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace DuoVia.Net.TcpIp
{
    public abstract class TcpClient<TInterface> : IDisposable where TInterface : class
    {
        private TInterface _proxy;

        public TInterface Proxy { get { return _proxy; } }

        public TcpClient(IPEndPoint endpoint)
        {
            _proxy = TcpProxy.CreateProxy<TInterface>(endpoint);
        }

        #region IDisposable Members

        public void Dispose()
        {
            (_proxy as TcpChannel).Dispose();
        }

        #endregion
    }
}
