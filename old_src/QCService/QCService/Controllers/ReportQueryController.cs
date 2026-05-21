// 报告查询控制器
// GET /api/v1/qc/query-report — 根据影像号查询报告数据

using Microsoft.AspNetCore.Mvc;
using QCService.Models;
using QCService.Services;

namespace QCService.Controllers;

[ApiController]
[Route("api/v1/qc")]
public class ReportQueryController : ControllerBase
{
    private readonly ReportQueryService _queryService;
    private readonly ILogger<ReportQueryController> _logger;

    public ReportQueryController(ReportQueryService queryService, ILogger<ReportQueryController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    /// <summary>
    /// 查询报告数据
    /// GET /api/v1/qc/query-report?accessNumber=CT202605190001
    /// 根据影像号查询 Oracle v_qc_report 视图，返回患者/检查/报告信息
    /// </summary>
    [HttpGet("query-report")]
    public async Task<IActionResult> QueryReport([FromQuery] string accessNumber)
    {
        // 校验参数
        if (string.IsNullOrWhiteSpace(accessNumber))
            return Ok(ApiResult.Error("影像号不能为空"));

        if (accessNumber.Length > 64)
            return Ok(ApiResult.Error("影像号过长"));

        try
        {
            var result = await _queryService.QueryByAccessNumberAsync(accessNumber);

            if (result == null)
            {
                _logger.LogWarning("报告未找到: AccessNumber={No}", accessNumber);
                return Ok(ApiResult.Success(null, "未查询到报告数据"));
            }

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报告查询失败: AccessNumber={No}", accessNumber);
            return Ok(ApiResult.Error("查询失败，请稍后重试"));
        }
    }

    /// <summary>
    /// 健康检查
    /// GET /api/v1/qc/health
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var dbConnected = await _queryService.CheckConnectionAsync();
        return Ok(ApiResult.Success(new
        {
            databaseConnected = dbConnected,
            timestamp = DateTime.Now
        }));
    }
}
