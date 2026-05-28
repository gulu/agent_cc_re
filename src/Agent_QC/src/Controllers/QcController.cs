using Microsoft.AspNetCore.Mvc;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Controllers;

[ApiController]
[Route("api/v1/qc")]
public class QcController : ControllerBase
{
    private readonly IQcService _qcService;

    public QcController(IQcService qcService)
    {
        _qcService = qcService;
    }

    [HttpPost("report")]
    public async Task<IActionResult> PostQcReport([FromBody] QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId))
            return Ok(AjaxResult.Error(400, "ReportId 不能为空"));

        if (string.IsNullOrWhiteSpace(request.Findings)
            && string.IsNullOrWhiteSpace(request.Impression))
            return Ok(AjaxResult.Error(400, "报告内容不能为空"));

        var result = await _qcService.ExecuteQcAsync(request);
        return Ok(result);
    }

    /// <summary>根据影像号查询 Oracle 报告原文</summary>
    [HttpGet("query-report")]
    public async Task<IActionResult> QueryReport([FromQuery] string accessNumber)
    {
        if (string.IsNullOrWhiteSpace(accessNumber))
            return Ok(AjaxResult.Error(400, "accessNumber 不能为空"));

        var reportQueryService = HttpContext.RequestServices.GetRequiredService<ReportQueryService>();
        var report = await reportQueryService.QueryByAccessNumberAsync(accessNumber);

        if (report == null)
            return Ok(AjaxResult.Error(404, $"未查询到影像号 {accessNumber} 对应的报告"));

        return Ok(AjaxResult.Success(report));
    }
}
