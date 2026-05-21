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
}
