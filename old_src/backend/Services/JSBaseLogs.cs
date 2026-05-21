// 日志工具 — 三级输出：文件 + 数据库 + 控制台
// 文件：自动按日期切割，保留 30 天
// 数据库：写入 qc_log 表，可查询追溯
// 控制台：即时输出，开发调试用

using System.Text;
using FreeSql;
using ReportQC.Entities;

namespace ReportQC.Services;

public static class JSBaseLogs
{
    private static IFreeSql? _fsql;
    private static string _logDir = string.Empty;
    private static readonly object _fileLock = new();
    private static bool _initialized = false;

    /// <summary>
    /// 初始化日志系统（在 Program.cs 启动时调用）
    /// </summary>
    public static void Initialize(IFreeSql? fsql = null, string? logDir = null)
    {
        if (_initialized) return;

        _fsql = fsql;
        _logDir = logDir ?? Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDir);

        _initialized = true;
        Info("日志系统初始化完成", "system");
    }

    // ── 公开方法 ──────────────────────────────────

    public static void WriteLog(Exception ex, string? source = null)
        => Write("ERROR", "system", ex.ToString(), source, null, null, ex.Message);

    public static void WriteLog(string message, string level = "INFO")
        => Write(level, "system", message, null, null, null, null);

    public static void Error(string message, string? source = null, string? reportId = null)
        => Write("ERROR", "system", message, source, reportId, null, null);

    public static void Warn(string message, string? source = null, string? reportId = null)
        => Write("WARN", "system", message, source, reportId, null, null);

    public static void Info(string message, string? category = "system", string? source = null)
        => Write("INFO", category, message, source, null, null, null);

    public static void Debug(string message, string? source = null)
        => Write("DEBUG", "system", message, source, null, null, null);

    /// <summary>兼容旧代码：JSLogManager 内部类</summary>
    public static class JSLogManager
    {
        public static void WriteLog(Exception ex) => JSBaseLogs.WriteLog(ex);
        public static void WriteLog(string message, string level = "INFO")
            => JSBaseLogs.WriteLog(message, level);
    }

    /// <summary>
    /// 记录质控操作日志
    /// </summary>
    public static void QcLog(string message, string? reportId = null, int? durationMs = null)
        => Write("INFO", "qc", message, "QcService", reportId, durationMs, null);

    // ── 内部实现 ──────────────────────────────────

    private static void Write(string level, string category, string message,
        string? source, string? reportId, int? durationMs, string? exception)
    {
        var timestamp = DateTime.Now;
        var line = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [{level,-5}] [{category}] {message}";

        // 1. 控制台输出
        if (level == "ERROR")
            Console.Error.WriteLine(line);
        else
            Console.WriteLine(line);

        // 2. 文件日志
        WriteToFile(timestamp, level, category, message, source, exception);

        // 3. 数据库日志（Fire-and-forget）
        if (_fsql != null)
            WriteToDb(_fsql, level, category, message, source, reportId, durationMs, exception);
    }

    private static void WriteToFile(DateTime timestamp, string level, string category,
        string message, string? source, string? exception)
    {
        var dateStr = timestamp.ToString("yyyy-MM-dd");
        var logFile = Path.Combine(_logDir, $"qc-{dateStr}.log");

        var sb = new StringBuilder();
        sb.Append($"[{timestamp:HH:mm:ss}] [{level,-5}] [{category}] ");
        if (!string.IsNullOrEmpty(source)) sb.Append($"[{source}] ");
        sb.Append(message);
        if (!string.IsNullOrEmpty(exception))
            sb.AppendLine().Append($"  └─ {exception}");

        lock (_fileLock)
        {
            try { File.AppendAllText(logFile, sb + Environment.NewLine); }
            catch { /* 文件写入失败不抛异常 */ }
        }
    }

    private static void WriteToDb(IFreeSql fsql, string level, string category,
        string message, string? source, string? reportId, int? durationMs, string? exception)
    {
        try
        {
            fsql.Insert(new QcLog
            {
                Level = level,
                Category = category,
                Message = Truncate(message, 500),
                Source = Truncate(source, 100),
                ReportId = reportId,
                Exception = exception != null ? Truncate(exception, 2000) : null,
                DurationMs = durationMs,
                CreatedAt = DateTime.Now
            }).ExecuteAffrows();
        }
        catch { /* 数据库写入失败不阻塞主流程 */ }
    }

    private static string? Truncate(string? s, int maxLen)
        => s?.Length > maxLen ? s[..maxLen] : s;

    /// <summary>清理 N 天前的日志文件</summary>
    public static void CleanOldLogs(int keepDays = 30)
    {
        try
        {
            if (!Directory.Exists(_logDir)) return;
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(_logDir, "qc-*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file).Replace("qc-", "");
                if (DateTime.TryParse(name, out var fd) && fd < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }

    /// <summary>查询数据库日志（供管理 API 使用）</summary>
    public static List<QcLog> QueryLogs(IFreeSql fsql, string? level = null,
        string? category = null, int page = 1, int pageSize = 50)
    {
        var query = fsql.Select<QcLog>().OrderByDescending(l => l.Id);
        if (!string.IsNullOrEmpty(level))
            query = query.Where(l => l.Level == level);
        if (!string.IsNullOrEmpty(category))
            query = query.Where(l => l.Category == category);
        return query.Skip((page - 1) * pageSize).Limit(pageSize).ToList();
    }
}
