﻿using System;
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
                _nodeServiceHost = new NpHost(_nodeService, MpiConsts.NodeServicePipeName);
                _nodeServiceHost.Open();

                _clusterService = new ClusterService();
                _clusterServiceHost = new TcpHost(_clusterService, Visor.Current.EndPoint);
                _clusterServiceHost.Open();

                Visor.Current.RegisterInstance(); //register self and with master or backup
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
                    Visor.Current.Dispose();
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

