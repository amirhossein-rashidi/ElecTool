using System;
using System.IO;
using System.Linq;
using System.Text;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core.Logging
{
    /// <summary>Daily log files. Logging never throws; old files (30+ days) are removed.</summary>
    public sealed class FileLogger : ILogger
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private const int KeepDays = 30;

        private readonly object _lock = new object();
        private readonly string _directory;

        public FileLogger(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Log directory is required.", nameof(directory));
            }

            _directory = directory;
            PruneOldFiles();
        }

        public string LogDirectory
        {
            get { return _directory; }
        }

        public string? OperationId { get; set; }

        private string CurrentFile
        {
            get { return Path.Combine(_directory, "electool-" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(" [").Append(level.ToString().ToUpperInvariant()).Append(']');

                string? op = OperationId;
                if (!string.IsNullOrEmpty(op))
                {
                    sb.Append(" [op:").Append(op).Append(']');
                }

                sb.Append(' ').Append(message);
                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append(exception.ToString());
                }

                sb.AppendLine();

                lock (_lock)
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(CurrentFile, sb.ToString(), Utf8NoBom);
                }
            }
            catch
            {
                // Logging must never break the tool.
            }
        }

        public void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public void Error(string message, Exception? exception = null)
        {
            Log(LogLevel.Error, message, exception);
        }

        public string[] ReadRecentLines(int maxLines)
        {
            try
            {
                if (maxLines <= 0 || !Directory.Exists(_directory))
                {
                    return Array.Empty<string>();
                }

                string? latest = Directory.GetFiles(_directory, "electool-*.log")
                    .OrderByDescending(f => f, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (latest == null)
                {
                    return Array.Empty<string>();
                }

                string[] lines;
                lock (_lock)
                {
                    lines = File.ReadAllLines(latest, Utf8NoBom);
                }

                return lines.Length <= maxLines
                    ? lines
                    : lines.Skip(lines.Length - maxLines).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private void PruneOldFiles()
        {
            try
            {
                if (!Directory.Exists(_directory))
                {
                    return;
                }

                DateTime limit = DateTime.Now.AddDays(-KeepDays);
                foreach (string file in Directory.GetFiles(_directory, "electool-*.log"))
                {
                    if (File.GetLastWriteTime(file) < limit)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Best effort only.
            }
        }
    }
}
