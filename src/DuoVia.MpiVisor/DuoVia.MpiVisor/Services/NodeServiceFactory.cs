﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.NamedPipes;

namespace DuoVia.MpiVisor.Services
{
    public interface INodeServiceFactory : IDisposable
    {
        bool IsInternalServer { get; set; }
        INodeService CreateConnection(Guid sessionId, bool runInSingleLocalProcess);
    }

    /// <summary>
    /// Factory creates node service proxy either internal or cluster node connected.
    /// </summary>
    internal class NodeServiceFactory : INodeServiceFactory
    {
        private InternalNodeService _localServerService = null;
        private NodeServiceProxy _svrProxy = null;
        private NpHost _localServerServiceHost = null;
        private ushort _agentId = 0;

        public bool IsInternalServer { get; set; }

        public NodeServiceFactory(ushort agentId)
        {
            _agentId = agentId;
        }

        public INodeService CreateConnection(Guid sessionId, bool runInSingleLocalProcess)
        {
            if (!runInSingleLocalProcess)
            {
                var npEndPoint = new NpEndPoint(MpiConsts.NodeServicePipeName, 2500);
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

            
            IsInternalServer = true;
            var pipeName = "dvmvs-" + string.Format("{0}-{1}", 0, sessionId);

            //host it locally on agent 0 - master agent
            if (_agentId == MpiConsts.MasterAgentId)
            {
                _localServerService = new InternalNodeService();
                _localServerServiceHost = new NpHost(pipeName);
                _localServerServiceHost.AddService<INodeService>(_localServerService);
                _localServerServiceHost.Open();
            }

            //connect to the local
            var localNpEndPoint = new NpEndPoint(pipeName, 2500);
            _svrProxy = new NodeServiceProxy(localNpEndPoint);
            if (_svrProxy.Ping(0) != 0) throw new Exception("unable to connect to regular server");
            return _svrProxy;
        }

        #region IDisposable members

        private bool _disposed = false;

        public void Dispose()
        {
            //MS recommended dispose pattern - prevents GC from disposing again
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true; //prevent second call to Dispose
                if (disposing)
                {
                    if (null != _svrProxy) _svrProxy.Dispose();
                    if (null != _localServerServiceHost) _localServerServiceHost.Dispose();
                }
            }
        }

        #endregion
    }
}
