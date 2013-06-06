using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Diagnostics;
using System.Configuration;
using DuoVia.MpiVisor.Services;
using DuoVia.Net.NamedPipes;
using DuoVia.Net.TcpIp;

namespace DuoVia.MpiVisor.Server
{
    internal class ServiceRunner
    {
        //local instances of hosted services here
        private INodeService _nodeService;
        private NpHost _nodeServiceHost;
        private IClusterService _clusterService;
        private TcpHost _clusterServiceHost;

        public void Start(string[] args)
        {
            try
            {
                //start hosting services here
                _nodeService = new NodeService();
                //specify "Users" to allow a local master agent to run against a service hosted by a domain user or other user
                _nodeServiceHost = new NpHost(_nodeService, MpiConsts.NodeServicePipeName);
                _nodeServiceHost.Open();

                _clusterService = new ClusterService();
                _clusterServiceHost = new TcpHost(_clusterService, ServerVisor.Current.EndPoint);
                _clusterServiceHost.Open();

                ServerVisor.Current.RegisterInstance(); //register self and with master or backup
            }
            catch (Exception e)
            {
                //do your exception handling thing
                e.ProcessUnhandledException("MpiVisorService");
            }
        }

        public void Stop()
        {
            //Add code that will execute on stop.
            if (true) //TODO is clean up required
            {
                try
                {
                    //clean up
                    _nodeServiceHost.Close();
                    _clusterServiceHost.Close();
                    ServerVisor.Current.Dispose();
                }
                catch (Exception e)
                {
                    //do your exception handling thing
                    e.ProcessUnhandledException("MpiVisorService");
                }
            }
        }
    }
}

