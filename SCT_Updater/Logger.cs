// Logger.cs
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SCT_Updater
{
    /// <summary>
    /// A simple static logger class to write debug messages to a file.
    /// </summary>
    public static class Log
    {
        private static readonly string _logPath = Path.Combine(Application.StartupPath, "logs/sct_updater.log");
        private static readonly object _lock = new object();

        static Log()
        {
            // Clear old log file on startup
            try
            {
                if (File.Exists(_logPath))
                {
                    File.Delete(_logPath);
                }
                Info("Logger initialized.");
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        private static void Write(string level, string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            try
            {
                string shortFile = Path.GetFileName(file);
                string logMessage = $"{DateTime.Now:HH:mm:ss.fff} [{level}] [{shortFile}:{line}] {message}{Environment.NewLine}";

                // Lock to prevent multiple threads writing at the same time
                lock (_lock)
                {
                    File.AppendAllText(_logPath, logMessage);
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        public static void Info(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write("INFO", message, file, line);
        }

        public static void Debug(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write("DEBUG", message, file, line);
        }

        public static void Warn(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write("WARN", message, file, line);
        }

        public static void Error(Exception ex, string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write("ERROR", $"{message} | Exception: {ex.Message}", file, line);
        }
    }
}