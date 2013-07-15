using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }

    public enum LogType
    {
        None,
        Console,
        File,
        Both
    }

    public interface ILogger : IDisposable
    {
        void Info(string formattedMessage, params object[] args);
        void Warning(string formattedMessage, params object[] args);
        void Error(string formattedMessage, params object[] args);
        void Debug(string formattedMessage, params object[] args);
        void LogMessage(string message);
        LogLevel LogLevel { get; set; }
        LogType LogType { get; set; }
        string[] ReadFile();
    }

    internal class Logger : ILogger
    {
        private LogLevel _logLevel = LogLevel.Info;
        private LogType _logType = LogType.File;
        private FileStream _fs;
        private StreamWriter _writer;
        private DateTime _startedAt = DateTime.Now;
        private string _logFileName;
        private bool isVisor = false;

        private void LazyInitialize()
        {
            isVisor = (null == Agent.Current);
            var fileName = isVisor ? "visor" : Agent.Current.Name;
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            _logFileName = (null == Agent.Current)
                ? Path.Combine(logDir,
                        string.Format("log-{0}-Visor.log", DateTime.Now.ToString("yyyyMMdd")))
                : Path.Combine(logDir,
                        string.Format("log-{0}-{1}.log", _startedAt.ToString("yyyyMMdd-hh-mm-ss-fff"), Agent.Current.Name));
        }

        private void AssureFileOpen()
        {
            ////if Log accessed prior to Agent creation by Visor switch to logging to agent log
            //if (isVisor && null != Agent.Current) 
            //{
            //    AssureFileClosed();
            //    Initialize();
            //}
            if (null == _writer)
            {
                LazyInitialize();
                _fs = File.Open(_logFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(_fs);
                _writer.AutoFlush = true;
            }
        }

        private void AssureFileClosed()
        {
            if (null != _writer)
            {
                _writer.Flush();
                _writer.Close();
                _writer = null;
                _fs = null;
            }
        }

        public LogLevel LogLevel
        {
            get
            {
                return _logLevel;
            }
            set
            {
                _logLevel = value;
            }
        }

        public LogType LogType
        {
            get
            {
                return _logType;
            }
            set
            {
                _logType = value;
                if (_logType != MpiVisor.LogType.File && _logType != MpiVisor.LogType.Both)
                {
                    AssureFileClosed();
                }
            }
        }

        public void LogMessage(string message)
        {
            if (_logType == MpiVisor.LogType.File || _logType == MpiVisor.LogType.Both)
            {
                AssureFileOpen();
                WriteLineToFile(message);
                // if is an agent and not master, shuttle log message back to master
                if (null != Agent.Current && Agent.Current.AgentId != MpiConsts.MasterAgentId)
                {
                    Agent.Current.MessageQueue.Send(new Message
                    {
                        FromId = Agent.Current.AgentId,
                        SessionId = Agent.Current.Session.SessionId,
                        ToId = MpiConsts.MasterAgentId,
                        MessageType = -999999, //reserved for internal log shuttle
                        Content = message
                    });
                }
            }
            else
            {
                AssureFileClosed();
            }
            if (_logType == MpiVisor.LogType.Console || _logType == MpiVisor.LogType.Both)
            {
                Console.WriteLine(message);
            }
        }

        private void WriteLineToFile(string message)
        {
            try
            {
                _writer.WriteLine(message);
                _writer.Flush();
            }
            catch (Exception e)
            {
                e.ProcessUnhandledException("MpiVisor");
            }
        }

        public string[] ReadFile()
        {
            if (_logType == MpiVisor.LogType.File || _logType == MpiVisor.LogType.Both)
            {
                AssureFileClosed();
                var lines = File.ReadAllLines(_logFileName);
                AssureFileOpen();
                return lines;
            }
            else
                return new string[0];
        }

        public void Info(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Info,
                string.Format(formattedMessage, args).Flatten()));
        }

        public void Warning(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Warning,
                string.Format(formattedMessage, args).Flatten()));
        }

        public void Error(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Error,
                string.Format(formattedMessage, args).Flatten()));
        }

        public void Debug(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Debug,
                string.Format(formattedMessage, args).Flatten()));
        }

        private string GetTimeStamp()
        {
            return DateTime.Now.ToString("yyyyMMdd-hh:mm:ss.fff");
        }

        private ushort GetAgentId()
        {
            if (Agent.Current != null)
                return Agent.Current.AgentId;
            else
                return MpiConsts.BroadcastAgentId;
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
                    AssureFileClosed();
                }
            }
        }

        #endregion

    }
}