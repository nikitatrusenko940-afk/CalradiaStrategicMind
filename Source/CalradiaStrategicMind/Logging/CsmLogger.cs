using System;
using System.IO;
using System.Reflection;

namespace CalradiaStrategicMind.Logging
{
    public static class CsmLogger
    {
        private const string LogDirectoryName = "Logs";
        private const string LogFileName = "CalradiaStrategicMind.log";

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(Exception exception)
        {
            Error("Unhandled exception", exception);
        }

        public static void Error(string message, Exception exception)
        {
            var fullMessage = exception == null
                ? message
                : $"{message} | {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}";

            Write("ERROR", fullMessage);
        }

        private static void Write(string level, string message)
        {
            try
            {
                var logDirectory = GetLogDirectory();
                Directory.CreateDirectory(logDirectory);

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(logDirectory, LogFileName), line);
            }
            catch
            {
                // Logging must never crash the game.
            }
        }

        private static string GetLogDirectory()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CalradiaStrategicMind", LogDirectoryName);
            }

            var directory = new DirectoryInfo(assemblyDirectory);
            if (directory.Name.Equals("Win64_Shipping_Client", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null
                && directory.Parent.Name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                && directory.Parent.Parent != null)
            {
                return Path.Combine(directory.Parent.Parent.FullName, LogDirectoryName);
            }

            return Path.Combine(assemblyDirectory, LogDirectoryName);
        }
    }
}
