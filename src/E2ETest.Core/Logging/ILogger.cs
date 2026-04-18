using System;

namespace E2ETest.Core.Logging
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public interface ILogger
    {
        void Log(LogLevel level, string message);
    }

    public sealed class ConsoleLogger : ILogger
    {
        private readonly LogLevel _min;
        public ConsoleLogger(LogLevel min) { _min = min; }
        public ConsoleLogger() : this(LogLevel.Info) { }

        public void Log(LogLevel level, string message)
        {
            if (level < _min) return;
            Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [" + level + "] " + message);
        }
    }

    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();
        public void Log(LogLevel level, string message) { }
    }
}
