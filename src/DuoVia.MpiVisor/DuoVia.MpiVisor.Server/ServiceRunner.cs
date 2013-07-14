using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Diagnostics;
using System.Configuration;
using DuoVia.MpiVisor.Services;
using DuoVia.Net.NamedPipes;
using DuoVia.Net.TcpIp;
using DuoVia.MpiVisor.Management;

namespace DuoVia.MpiVisor.Server
{
    internal class ServiceRunner
    {
        //local instances of hosted services here
        private INodeService _nodeService;
        private NpHost _nodeServiceHost;
        private IClusterService _clusterService;
        private TcpHost _clusterServiceHost;
        private IManagementService _managementService;
        private TcpHost _managementServiceHost;

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

                //create management service if configured
                var config = System.Configuration.ConfigurationManager.AppSettings["ManagementClusterNodeAddress"];
                if (!string.IsNullOrWhiteSpace(config))
                {
                    var parts = config.Split(',');
                    var endPoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                    _managementService = new ManagementService();
                    _managementServiceHost = new TcpHost(_managementService, endPoint);
                    _managementServiceHost.Open();
                }

                //register self and with master or backup
                ServerVisor.Current.RegisterInstance(); 
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
                    ServerVisor.Current.Dispose();
                    _nodeServiceHost.Close();
                    _clusterServiceHost.Close();
                    if (null != _managementServiceHost) _managementServiceHost.Close();
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

