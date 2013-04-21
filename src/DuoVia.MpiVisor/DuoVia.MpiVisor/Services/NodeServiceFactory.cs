using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.NamedPipes;

namespace DuoVia.MpiVisor.Services
{
    /// <summary>
    /// Factory creates node service proxy either internal or cluster node connected.
    /// </summary>
    internal sealed class NodeServiceFactory : IDisposable
    {
        private InternalNodeService _localServerService = null;
        private NodeServiceProxy _svrProxy = null;
        private NpHost _localServerServiceHost = null;

        public bool IsInternalServer { get; set; }

        public INodeService CreateConnection(string agentName, bool useInternalNodeService)
        {
            if (!useInternalNodeService)
            {
                var npEndPoint = new NpEndPoint(MpiConsts.NodeServicePipeName);
                try
                {
                    //connect to the server process 
                    _svrProxy = new NodeServiceProxy(npEndPoint);
                    if (_svrProxy.Ping(0) != 0) throw new Exception("unable to connect to regular server");
                    IsInternalServer = false;
                    return _svrProxy;
                }
                catch
                {
                    //any failure to connect and we just fall through and go local
                }
            }

            //host it locally
            IsInternalServer = true;
            _localServerService = new InternalNodeService();
            var pipeName = "dvmvs-" + agentName; //unique for this agent/session
            _localServerServiceHost = new NpHost(_localServerService, pipeName);
            _localServerServiceHost.Open();

            //connect to the local
            var localNpEndPoint = new NpEndPoint(pipeName);
            _svrProxy = new NodeServiceProxy(localNpEndPoint);
            if (_svrProxy.Ping(0) != 0) throw new Exception("unable to connect to regular server");
            return _svrProxy;
        }

        public void Dispose()
        {
            if (null != _svrProxy) _svrProxy.Dispose();
            if (null != _localServerServiceHost) _localServerServiceHost.Close();
        }
    }
}
