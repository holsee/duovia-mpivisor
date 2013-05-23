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

namespace DuoVia.Net
{
    public abstract class Channel : IDisposable
    {
        protected BinaryReader _binReader;
        protected BinaryWriter _binWriter;
        protected Stream _stream;
        protected BinaryFormatter _formatter = new BinaryFormatter();
        private ParameterTransferHelper _parameterTransferHelper = new ParameterTransferHelper();
        private List<MethodSyncInfo> _syncInfos;

        /// <summary>
        /// This method asks the server for a list of identifiers paired with method
        /// names and -parameter types. This is used when invoking methods server side.
        /// </summary>
        protected void SyncInterface()
        {
            //write the message type
            _binWriter.Write((int)MessageType.SyncInterface);

            //read sync data
            var ms = new MemoryStream(_binReader.ReadBytes(_binReader.ReadInt32()));
            _syncInfos = (List<MethodSyncInfo>)_formatter.Deserialize(ms);
        }

        /// <summary>
        /// Closes the connection to the server
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Invokes the method with the specified parameters.
        /// </summary>
        /// <param name="parameters">Parameters for the method call</param>
        /// <returns>An array of objects containing the return value (index 0) and the parameters used to call
        /// the method, including any marked as "ref" or "out"</returns>
        protected object[] InvokeMethod(params object[] parameters)
        {
            //write the message type
            _binWriter.Write((int)MessageType.MethodInvocation);

            //find the mathing server side method ident
            var callingMethod = (new StackFrame(1)).GetMethod();
            var methodName = callingMethod.Name;
            var methodParams = callingMethod.GetParameters();
            var ident = -1;
            foreach (var si in _syncInfos)
            {
                //first of all the method names must match
                if (si.MethodName == methodName)
                {
                    //second of all the parameter types and -count must match
                    if (methodParams.Length == si.ParameterTypes.Length)
                    {
                        var matchingParameterTypes = true;
                        for (int i = 0; i < methodParams.Length; i++)
                            if (!methodParams[i].ParameterType.FullName.Equals(si.ParameterTypes[i].FullName))
                            {
                                matchingParameterTypes = false;
                                break;
                            }
                        if (matchingParameterTypes)
                        {
                            ident = si.MethodIdent;
                            break;
                        }
                    }
                }
            }

            if (ident < 0)
                throw new Exception(string.Format("Cannot match method '{0}' to its server side equivalent", callingMethod.Name));

            //write the method ident to the server
            _binWriter.Write(ident);

            //send the parameters
            _parameterTransferHelper.SendParameters(_binWriter, parameters);

            _binWriter.Flush();
            _stream.Flush();

            // Read the result of the invocation.
            MessageType messageType = (MessageType)_binReader.ReadInt32();
            if (messageType == MessageType.UnknownMethod)
                throw new Exception("Unknown method.");

            object[] outParams = _parameterTransferHelper.ReceiveParameters(_binReader);

            if (messageType == MessageType.ThrowException)
                throw (Exception)outParams[0];

            return outParams;
        }

        #region IDisposable Members

        public virtual void Dispose()
        {
            _binWriter.Write((int)MessageType.TerminateConnection);
            _binWriter.Flush();
            _stream.Flush();
            _binWriter.Close();
            _binReader.Close();
            _stream.Close();
        }

        #endregion
    }
}
