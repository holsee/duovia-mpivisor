using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuoVia.Net.NamedPipes
{
    public class PipeClientConnectionEventArgs : EventArgs
    {
        public PipeClientConnectionEventArgs(NamedPipeServerStream pipeStream)
        {
            this.PipeStream = pipeStream;
        }
        public NamedPipeServerStream PipeStream { get; set; }
    }

    public class NpListener
    {
        private bool running;
        private Thread runningThread;
        private EventWaitHandle terminateHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private int _maxConnections = 254;
        private PipeSecurity _pipeSecurity = null;
        public string PipeName { get; set; }
        public event EventHandler<PipeClientConnectionEventArgs> RequestReieved;

        public NpListener(string pipeName, int maxConnections = 254, string[] allowedUsers = null)
        {
            if (maxConnections > 254) maxConnections = 254;
            _maxConnections = maxConnections;
            this.PipeName = pipeName;
            if (null != allowedUsers && allowedUsers.Length > 0)
            {
                //create PipeSecurity
                var pipeRules = new List<PipeAccessRule>();
                foreach (var user in allowedUsers)
                {
                    pipeRules.Add(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
                }
                //add default rules back in
                pipeRules.Add(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
                pipeRules.Add(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));

                _pipeSecurity = new PipeSecurity();
                foreach (var rule in pipeRules)
                {
                    _pipeSecurity.AddAccessRule(rule);
                }
            }
        }

        public void Start()
        {
            running = true;
            runningThread = new Thread(ServerLoop);
            runningThread.Start();
        }

        public void Stop()
        {
            if (running)
            {
                running = false;
                //make fake connection to terminate the waiting stream
                try
                {
                    using (var client = new NamedPipeClientStream(PipeName))
                    {
                        client.Connect(50);
                    }
                }
                catch { }

                terminateHandle.WaitOne();
            }
        }

        private void ServerLoop()
        {
            while (running)
            {
                ProcessNextClient();
            }
            terminateHandle.Set();
        }

        private void ProcessClientThread(object o)
        {
            var pipeStream = (NamedPipeServerStream)o;
            try
            {
                if (this.RequestReieved != null) //has event subscribers
                {
                    var args = new PipeClientConnectionEventArgs(pipeStream);
                    RequestReieved(this, args);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: {0}", e.Message);
                throw; //no suppression, just logging
            }
            finally
            {
                if (pipeStream.IsConnected) pipeStream.Close();
                pipeStream.Dispose();
            }
        }

        public void ProcessNextClient()
        {
            try
            {
                var pipeStream = (_pipeSecurity == null)
                    ? new NamedPipeServerStream(PipeName, PipeDirection.InOut, _maxConnections, PipeTransmissionMode.Byte, PipeOptions.WriteThrough, 1024, 1024)
                    : new NamedPipeServerStream(PipeName, PipeDirection.InOut, _maxConnections, PipeTransmissionMode.Byte, PipeOptions.WriteThrough, 1024, 1024, _pipeSecurity);

                pipeStream.WaitForConnection();

                //spawn a new thread for each request and continue waiting
                var t = new Thread(ProcessClientThread);
                t.Start(pipeStream);
            }
            catch
            {
                //If there are no more avail connections (254 is in use already) then just keep looping until one is avail
            }
        }
    }

    // Defines the data protocol for reading and writing strings on our stream
    public class StreamString
    {
        private Stream ioStream;
        private UnicodeEncoding streamEncoding;

        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public string ReadString()
        {
            int len = 0;

            len = ioStream.ReadByte() * 256;
            len += ioStream.ReadByte();
            byte[] inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            return streamEncoding.GetString(inBuffer);
        }

        public int WriteString(string outString)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString);
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int)UInt16.MaxValue;
            }
            ioStream.WriteByte((byte)(len / 256));
            ioStream.WriteByte((byte)(len & 255));
            ioStream.Write(outBuffer, 0, len);
            ioStream.Flush();

            return outBuffer.Length + 2;
        }
    }

    // Contains the method executed in the context of the impersonated user
    public class ReadFileToStream
    {
        private string fn;
        private StreamString ss;

        public ReadFileToStream(StreamString str, string filename)
        {
            fn = filename;
            ss = str;
        }

        public void Start()
        {
            string contents = File.ReadAllText(fn);
            ss.WriteString(contents);
        }
    }
}
