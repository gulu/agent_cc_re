using System;
using System.IO;
using System.Text;

namespace QCClient
{
    /// <summary>轻量级日志工具，替代 Serilog（避免 DLL 版本冲突）</summary>
    public static class Logger
    {
        private static string _logDir;
        private static readonly object _lock = new object();

        public static void Init(string logPathPattern)
        {
            _logDir = Path.GetDirectoryName(logPathPattern);
            if (!string.IsNullOrEmpty(_logDir) && !Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
        }

        public static void Info(string msg) { Write("INFO", msg); }
        public static void Warning(string msg) { Write("WARN", msg); }
        public static void Warning(Exception ex, string msg) { Write("WARN", $"{msg} | {ex.Message}"); }
        public static void Error(string msg) { Write("ERROR", msg); }
        public static void Error(Exception ex, string msg) { Write("ERROR", $"{msg} | {ex.Message}"); }
        public static void Error(Exception ex, string msg, params object[] args)
        {
            string formatted = msg;
            if (args != null && args.Length > 0)
            {
                try { formatted = string.Format(msg, args); } catch { formatted = msg; }
            }
            Error(ex, formatted);
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            // 控制台
            try { Console.WriteLine(line); } catch { }

            // 文件
            if (!string.IsNullOrEmpty(_logDir))
            {
                lock (_lock)
                {
                    try
                    {
                        string date = DateTime.Now.ToString("yyyyMMdd");
                        string path = Path.Combine(_logDir, $"qcclient-{date}.log");
                        File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                    }
                    catch { }
                }
            }
        }
    }
}
