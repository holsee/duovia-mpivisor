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

    public static class Log
    {
        private static object syncRoot = new object();
        private static LogLevel _logLevel = LogLevel.Info;
        private static LogType _logType = LogType.File;
        private static FileStream _fs;
        private static StreamWriter _writer;

        private static readonly DateTime _startedAt = DateTime.Now;
        public static readonly string LogFileName;

        static Log()
        {
            var logFileName = (null == Agent.Current) ? "visor" : Agent.Current.Name;
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            Log.LogFileName = (null == Agent.Current)
                ? Path.Combine(logDir,
                        string.Format("log-{0}-Visor.log", DateTime.Now.ToString("yyyyMMdd")))
                : Path.Combine(logDir, 
                        string.Format("log-{0}-{1}.log", _startedAt.ToString("yyyyMMdd-hh-mm-ss-fff"), Agent.Current.Name));
        }

        private static void AssureFileOpen()
        {
            if (null == _writer)
            {
                _fs = File.Open(LogFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(_fs);
                _writer.AutoFlush = true;
            }
        }

        private static void AssureFileClosed()
        {
            if (null != _writer)
            {
                _writer.Flush();
                _writer.Close();
                _writer = null;
                _fs = null;
            }
        }

        public static void Close()
        {
            lock (syncRoot)
            {
                AssureFileClosed();
            }
        }

        public static LogLevel LogLevel
        {
            get
            {
                return _logLevel;
            }
            set 
            {
                lock(syncRoot)
                {
                    _logLevel = value;
                }
            }
        }

        public static LogType LogType
        {
            get
            {
                return _logType;
            }
            set
            {
                lock(syncRoot)
                {
                    _logType = value;
                    if (_logType == MpiVisor.LogType.File || _logType == MpiVisor.LogType.Both)
                    {
                        AssureFileOpen();
                    }
                    else
                    {
                        AssureFileClosed();
                    }
                }
            }
        }

        public static void LogMessage(string message)
        {
            lock (syncRoot)
            {
                if (_logType == MpiVisor.LogType.File || _logType == MpiVisor.LogType.Both)
                {
                    AssureFileOpen();
                    WriteLineToFile(message);
                    // if is an agent and not master, shuttle log message back to master
                    if (null != Agent.Current && Agent.Current.AgentId != MpiConsts.MasterAgentId)
                    {
                        Agent.Current.Send(new Message
                            {
                                FromId = Agent.Current.AgentId,
                                SessionId = Agent.Current.SessionId,
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
        }

        private static void WriteLineToFile(string message)
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

        public static string[] ReadFile()
        {
            lock (syncRoot)
            {
                if (_logType == MpiVisor.LogType.File || _logType == MpiVisor.LogType.Both)
                {
                    AssureFileClosed();
                    var lines = File.ReadAllLines(LogFileName);
                    AssureFileOpen();
                    return lines;
                }
                else
                    return new string[0];
            }
        }

        public static void Info(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Info, 
                string.Format(formattedMessage, args).Flatten()));
        }

        public static void Warning(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Warning,
                string.Format(formattedMessage, args).Flatten()));
        }

        public static void Error(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}",
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Error,
                string.Format(formattedMessage, args).Flatten()));
        }

        public static void Debug(string formattedMessage, params object[] args)
        {
            LogMessage(string.Format("{0}\t{1}\t{2}\t{3}", 
                GetTimeStamp(),
                GetAgentId(),
                LogLevel.Debug,
                string.Format(formattedMessage, args).Flatten()));
        }

        private static string GetTimeStamp()
        {
            return DateTime.Now.ToString("yyyyMMdd-hh:mm:ss.fff");
        }

        private static ushort GetAgentId()
        {
            if (Agent.Current != null)
                return Agent.Current.AgentId;
            else
                return MpiConsts.BroadcastAgentId;
        }
    }
}
