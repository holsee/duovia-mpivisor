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

namespace DuoVia.Net.TcpIp
{
    public class TcpChannel : Channel, IDisposable
    {
        private TcpClient _client;

        /// <summary>
        /// Creates a connection to the concrete object handling method calls on the server side
        /// </summary>
        /// <param name="endpoint"></param>
        public TcpChannel(IPEndPoint endpoint)
        {
            _client = new TcpClient(AddressFamily.InterNetwork);
            _client.Connect(endpoint);
            _client.NoDelay = true;
            _stream = _client.GetStream();
            _binReader = new BinaryReader(_stream);
            _binWriter = new BinaryWriter(_stream);
            _formatter = new BinaryFormatter();
            SyncInterface();
        }

        #region IDisposable Members

        public override void Dispose()
        {
            //write terminate connection message and dispose of stream and read/writer first
            base.Dispose();
            _client.Close();
        }

        #endregion
    }
}
