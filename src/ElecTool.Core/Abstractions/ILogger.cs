using System;

namespace ElecTool.Core.Abstractions
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Error/event log. Implementations must never throw.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Folder that contains the log files.</summary>
        string LogDirectory { get; }

        /// <summary>
        /// Optional id of the operation currently running. It is written on every log line,
        /// so later stages can trace everything one operation did.
        /// </summary>
        string? OperationId { get; set; }

        void Log(LogLevel level, string message, Exception? exception = null);
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? exception = null);

        /// <summary>Returns the last lines of the newest log file (empty array if none).</summary>
        string[] ReadRecentLines(int maxLines);
    }
}
