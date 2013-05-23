using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection.Emit;
using System.IO.Compression;

namespace DuoVia.Net.NamedPipes
{
    public class NpHost : IDisposable
    {
        private bool _isOpen = false;

        private NpListener _listener;
        //private List<TcpClient> _clients;

        private bool _continueListening;
        private object _continueListeningLock = new object();
        private object _singletonInstance;
        private string _pipeName;
        private bool _useThreadPool = false;
        private Dictionary<int, MethodInfo> _interfaceMethods;
        private Dictionary<int, bool[]> _methodParametersByRef;
        private ParameterTransferHelper _parameterTransferHelper = new ParameterTransferHelper();

        private bool Continue
        {
            get
            {
                lock (_continueListeningLock)
                {
                    return _continueListening;
                }
            }
            set
            {
                lock (_continueListeningLock)
                {
                    _continueListening = value;
                }
            }
        }

        /// <summary>
        /// Get or set whether the host should use regular or thread pool threads.
        /// </summary>
        public bool UseThreadPool
        {
            get { return _useThreadPool; }
            set
            {
                if (_isOpen)
                    throw new Exception("The host is already open");
                _useThreadPool = value;
            }
        }

        /// <summary>
        /// Constructs an instance of the host and starts listening for incoming connections.
        /// All listener threads are regular background threads.
        /// 
        /// NOTE: the instance created from the specified type is not automatically thread safe!
        /// </summary>
        /// <param name="remotedType">The remoted type. This must have a default constructor</param>
        /// <param name="pipeName">The pipe name for incoming requests</param>
        public NpHost(Type remotedType, string pipeName)
            : this(Activator.CreateInstance(remotedType), pipeName)
        {
        }

        /// <summary>
        /// Constructs an instance of the host and starts listening for incoming connections.
        /// All listener threads are regular background threads.
        /// 
        /// NOTE: the instance is not automatically thread safe!
        /// </summary>
        /// <param name="singletonInstance">The singleton instance of the service</param>
        /// <param name="pipeName">The pipe name for incoming requests</param>
        public NpHost(object singletonInstance, string pipeName)
        {
            _pipeName = pipeName;
            _listener = new NpListener(_pipeName); //new TcpListener(_endPoint);
            _listener.RequestReieved += ClientConnectionMade;
            _continueListening = true;
            _singletonInstance = singletonInstance;
            CreateMethodMap();
        }

        /// <summary>
        /// Loads all methods from interfaces and assigns an identifier
        /// to each. These are later synchronized with the client.
        /// </summary>
        private void CreateMethodMap()
        {
            var interfaces = _singletonInstance.GetType().GetInterfaces();
            _interfaceMethods = new Dictionary<int, MethodInfo>();
            _methodParametersByRef = new Dictionary<int, bool[]>();
            var currentMethodIdent = 0;
            foreach (var interfaceType in interfaces)
            {
                var methodInfos = interfaceType.GetMethods();
                foreach (var mi in methodInfos)
                {
                    _interfaceMethods.Add(currentMethodIdent, mi);
                    var parameterInfos = mi.GetParameters();
                    var isByRef = new bool[parameterInfos.Length];
                    for (int i = 0; i < isByRef.Length; i++)
                        isByRef[i] = parameterInfos[i].ParameterType.IsByRef;
                    _methodParametersByRef.Add(currentMethodIdent, isByRef);
                    currentMethodIdent++;
                }
            }
        }

        /// <summary>
        /// Gets the end point this host is listening on
        /// </summary>
        public string PipeName
        {
            get { return _pipeName; }
        }

        /// <summary>
        /// Opens the host and starts a listener thread. This listener thread spawns a new thread (or uses a
        /// thread pool thread) for each incoming connection.
        /// </summary>
        public void Open()
        {
            _listener.Start(); //start listening in the background
            _isOpen = true;
        }

        /// <summary>
        /// Closes the host and calls Dispose().
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// This method handles all requests on separate thread per client connection.
        /// There is one thread running this method for each connected client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ClientConnectionMade(object sender, PipeClientConnectionEventArgs args)
        {
            Stream stream = args.PipeStream;
            BinaryReader binReader = new BinaryReader(stream);
            BinaryWriter binWriter = new BinaryWriter(stream);
            bool doContinue = true;
            do
            {
                try
                {
                    MemoryStream ms;
                    BinaryFormatter formatter = new BinaryFormatter();
                    //read message type
                    MessageType messageType = (MessageType)binReader.ReadInt32();
                    switch (messageType)
                    {
                        case MessageType.SyncInterface:
                            //Create a list of sync infos from the dictionary
                            var syncInfos = new List<MethodSyncInfo>();
                            foreach (var kvp in _interfaceMethods)
                            {
                                var parameters = kvp.Value.GetParameters();
                                var parameterTypes = new Type[parameters.Length];
                                for (var i = 0; i < parameters.Length; i++)
                                    parameterTypes[i] = parameters[i].ParameterType;
                                syncInfos.Add(new MethodSyncInfo { MethodIdent = kvp.Key, MethodName = kvp.Value.Name, ParameterTypes = parameterTypes });
                            }

                            //send the sync data back to the client
                            ms = new MemoryStream();
                            formatter.Serialize(ms, syncInfos);
                            ms.Seek(0, SeekOrigin.Begin);
                            binWriter.Write((int)ms.Length);
                            binWriter.Write(ms.ToArray());
                            binWriter.Flush();
                            stream.Flush();
                            break;
                        case MessageType.MethodInvocation:
                            //read the method identifier
                            int methodHashCode = binReader.ReadInt32();
                            if (_interfaceMethods.ContainsKey(methodHashCode))
                            {
                                var method = _interfaceMethods[methodHashCode];
                                var isByRef = _methodParametersByRef[methodHashCode];

                                //read parameter data
                                var parameters = _parameterTransferHelper.ReceiveParameters(binReader);

                                //invoke the method
                                object[] returnParameters;
                                var returnMessageType = MessageType.ReturnValues;
                                try
                                {
                                    object returnValue = method.Invoke(_singletonInstance, parameters);
                                    //the result to the client is the return value (null if void) and the input parameters
                                    returnParameters = new object[1 + parameters.Length];
                                    returnParameters[0] = returnValue;
                                    for (int i = 0; i < parameters.Length; i++)
                                        returnParameters[i + 1] = isByRef[i] ? parameters[i] : null;
                                }
                                catch (Exception ex)
                                {
                                    //an exception was caught. Rethrow it client side
                                    returnParameters = new object[] { ex };
                                    returnMessageType = MessageType.ThrowException;
                                }

                                //send the result back to the client
                                // (1) write the message type
                                binWriter.Write((int)returnMessageType);
                                // (2) write the return parameters
                                _parameterTransferHelper.SendParameters(binWriter, returnParameters);
                            }
                            else
                                binWriter.Write((int)MessageType.UnknownMethod);

                            //flush
                            binWriter.Flush();
                            stream.Flush();
                            break;
                        case MessageType.TerminateConnection:
                            doContinue = false;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception) //do not resume operation on this thread if any errors are unhandled.
                {
                    doContinue = false;
                }
            }
            while (doContinue);
            binReader.Close(); //closes underlying stream
        }

        #region IDisposable Members

        public void Dispose()
        {
            _isOpen = false;
            Continue = false;
            _listener.Stop();
        }

        #endregion
    }
}
