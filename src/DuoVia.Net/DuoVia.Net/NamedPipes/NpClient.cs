using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace DuoVia.Net.NamedPipes
{
    public abstract class NpClient<TInterface> : IDisposable where TInterface : class
    {
        private TInterface _proxy;

        public TInterface Proxy { get { return _proxy; } }

        public NpClient(NpEndPoint npAddress)
        {
            _proxy = NpProxy.CreateProxy<TInterface>(npAddress);
        }

        #region IDisposable Members

        public void Dispose()
        {
            (_proxy as NpChannel).Dispose();
        }

        #endregion
    }
}
