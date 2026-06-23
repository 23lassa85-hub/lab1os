using System;

namespace DirectoryCopier.Models
{
    public class LogEntry
    {
        public DateTime Time { get; }
        public string Message { get; }
        public LogType Type { get; }

        public LogEntry(string message, LogType type = LogType.Info)
        {
            Time = DateTime.Now;
            Message = message;
            Type = type;
        }
    }

    public enum LogType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
