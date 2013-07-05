using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DuoVia.MpiVisor
{
    /// <summary>
    /// Provides simple thread safe access to ILogger.
    /// </summary>
    public static class Log
    {
        private static object syncRoot = new object();
        private static ILogger _logger = null;

        static Log()
        {
            _logger = _logger ?? new Logger();
        }

        /// <summary>
        /// Allows injection of a logger other than the default Logger.
        /// </summary>
        /// <param name="logger"></param>
        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public static void Close()
        {
            lock (syncRoot)
            {
                _logger.Dispose();
            }
        }

        public static LogLevel LogLevel
        {
            get
            {
                return _logger.LogLevel;
            }
            set 
            {
                lock(syncRoot)
                {
                    _logger.LogLevel = value;
                }
            }
        }

        public static LogType LogType
        {
            get
            {
                return _logger.LogType;
            }
            set
            {
                lock(syncRoot)
                {
                    _logger.LogType = value;
                }
            }
        }

        public static void LogMessage(string message)
        {
            lock (syncRoot)
            {
                _logger.LogMessage(message);
            }
        }

        public static string[] ReadFile()
        {
            lock (syncRoot)
            {
                return _logger.ReadFile();
            }
        }

        public static void Info(string formattedMessage, params object[] args)
        {
            lock (syncRoot)
            {
                _logger.Info(formattedMessage, args);
            }
        }

        public static void Warning(string formattedMessage, params object[] args)
        {
            lock (syncRoot)
            {
                _logger.Warning(formattedMessage, args);
            }
        }

        public static void Error(string formattedMessage, params object[] args)
        {
            lock (syncRoot)
            {
                _logger.Error(formattedMessage, args);
            }
        }

        public static void Debug(string formattedMessage, params object[] args)
        {
            lock (syncRoot)
            {
                _logger.Debug(formattedMessage, args);
            }
        }
    }
}
