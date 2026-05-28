using System;
using System.IO;
using Serilog;
using Serilog.Core;

namespace QCClient
{
    public static class Logger
    {
        private static Serilog.Core.Logger _log;

        public static void Init(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }

            _log = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(path, rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static void Info(string msg) => _log?.Information(msg);
        public static void Warning(string msg) => _log?.Warning(msg);
        public static void Warning(Exception ex, string msg) => _log?.Warning(ex, msg);
        public static void Error(string msg) => _log?.Error(msg);
        public static void Error(Exception ex, string msg) => _log?.Error(ex, msg);
    }
}
