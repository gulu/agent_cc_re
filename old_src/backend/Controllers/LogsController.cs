// 日志查询 API
// GET /api/v1/logs — 日志列表
// GET /api/v1/logs/stats — 日志统计

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/logs")]
public class LogsController : BaseController
{
    private readonly IFreeSql _fsql;

    public LogsController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>日志列表（分页+筛选）</summary>
    [HttpGet]
    public IActionResult GetList(
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = JSBaseLogs.QueryLogs(_fsql, level, category, page, pageSize);
            var total = _fsql.Select<ReportQC.Entities.QcLog>().Count();
            return Success(new { items = result, total, page, pageSize });
        }
        catch (System.Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return Error($"查询日志失败：{ex.Message}");
        }
    }

    /// <summary>日志统计</summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        try
        {
            var stats = new
            {
                total = _fsql.Select<ReportQC.Entities.QcLog>().Count(),
                byLevel = new
                {
                    error = _fsql.Select<ReportQC.Entities.QcLog>().Where(l => l.Level == "ERROR").Count(),
                    warn = _fsql.Select<ReportQC.Entities.QcLog>().Where(l => l.Level == "WARN").Count(),
                    info = _fsql.Select<ReportQC.Entities.QcLog>().Where(l => l.Level == "INFO").Count(),
                },
                byCategory = _fsql.Select<ReportQC.Entities.QcLog>()
                    .ToList(l => l.Category)
                    .GroupBy(c => c)
                    .Select(g => new { category = g.Key, count = g.Count() })
                    .ToList(),
            };
            return Success(stats);
        }
        catch (System.Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return Error($"查询统计失败：{ex.Message}");
        }
    }
}
