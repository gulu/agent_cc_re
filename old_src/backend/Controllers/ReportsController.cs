// 质控记录查询 API
// GET /api/v1/qc/reports — 列表
// GET /api/v1/qc/reports/{id} — 详情
// GET /api/v1/qc/reports/stats — 统计

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/qc")]
public class ReportsController : BaseController
{
    private readonly IFreeSql _fsql;

    public ReportsController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>质控记录列表（分页+筛选）</summary>
    [HttpGet("reports")]
    public IActionResult GetList(
        [FromQuery] string? reportId,
        [FromQuery] string? reportType,
        [FromQuery] bool? passed,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        DateTime? df = null, dt = null;
        if (DateTime.TryParse(dateFrom, out var d1)) df = d1;
        if (DateTime.TryParse(dateTo, out var d2)) dt = d2;

        var result = QcQueryService.GetReportList(_fsql, reportId, reportType,
            passed, df, dt, page, pageSize);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>质控记录详情</summary>
    [HttpGet("reports/{id}")]
    public IActionResult GetDetail(long id)
    {
        var result = QcQueryService.GetReportDetail(_fsql, id);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>质控统计概览</summary>
    [HttpGet("reports/stats")]
    public IActionResult GetStats()
    {
        var result = QcQueryService.GetStats(_fsql);
        return Ok(JsonConvert.SerializeObject(result));
    }
}
