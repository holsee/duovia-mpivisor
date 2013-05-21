using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.IO.Pipes;

namespace DuoVia.Net.NamedPipes
{
    public class NpChannel : Channel, IDisposable
    {
        private NamedPipeClientStream _clientStream;

        /// <summary>
        /// Creates a connection to the concrete object handling method calls on the pipeName server side
        /// </summary>
        /// <param name="pipeName"></param>
        public NpChannel(NpEndPoint npEndPoint)
        {
            _clientStream = new NamedPipeClientStream(npEndPoint.ServerName, npEndPoint.PipeName, PipeDirection.InOut);
            _clientStream.Connect(npEndPoint.ConnectTimeOutMs);
            _stream = _clientStream;
            _binReader = new BinaryReader(_clientStream);
            _binWriter = new BinaryWriter(_clientStream);
            SyncInterface();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }

    public class NpEndPoint
    {
        public NpEndPoint(string pipeName, int connectTimeOutMs = 500) : this(".", pipeName, connectTimeOutMs)
        {
        }

        public NpEndPoint(string serverName, string pipeName, int connectTimeOutMs = 500)
        {
            this.ServerName = serverName;
            this.PipeName = pipeName;
            this.ConnectTimeOutMs = connectTimeOutMs;
        }

        public string ServerName { get; set; }
        public string PipeName { get; set; }
        public int ConnectTimeOutMs { get; set; }
    }
}
