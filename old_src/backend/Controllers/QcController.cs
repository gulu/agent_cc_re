// 质控 API 控制器
// 路由：/api/v1/qc/report

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Models;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/qc")]
public class QcController : BaseController
{
    private readonly IFreeSql _fsql;

    public QcController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>
    /// 执行报告质控
    /// POST /api/v1/qc/report
    /// 完整的报告信息、患者信息、临床诊断、检查申请信息以 JSON 格式传入
    /// </summary>
    [HttpPost("report")]
    public IActionResult ReportQc([FromBody] QcRequest request)
    {
        // ── 请求日志 ──
        JSBaseLogs.Info($"[QC-IN] reportId={request.ReportId}, findingsLen={request.Findings?.Length ?? 0}, impressionLen={request.Impression?.Length ?? 0}, examPart={request.ExamPart}, reportType={request.ReportType}, patientGender={request.PatientGender}", "qc_api");

        // ── 校验 ──
        if (!request.ValidateReportId(out var idError))
        {
            JSBaseLogs.Warn($"[QC-REJECT] {idError}", "qc_api", request.ReportId);
            return Error(idError);
        }

        if (!request.ValidateContent(out var contentError))
        {
            JSBaseLogs.Warn($"[QC-REJECT] {contentError}", "qc_api", request.ReportId);
            return Error(contentError);
        }

        // ── 执行为控 ──
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = QcService.ExecuteQc(_fsql, request);
        sw.Stop();

        // ── 响应日志 ──
        var respJson = JsonConvert.SerializeObject(result);
        var preview = respJson.Length > 500 ? respJson[..500] + "..." : respJson;
        JSBaseLogs.Info($"[QC-OUT] reportId={request.ReportId}, durationMs={sw.ElapsedMilliseconds}, respPreview={preview}", "qc_api");

        // 记录质控日志
        JSBaseLogs.QcLog($"报告 {request.ReportId} 质控完成",
            request.ReportId, (int)sw.ElapsedMilliseconds);

        // ★ 使用 ASP.NET Core 内置 System.Text.Json（CamelCase）统一序列化
        // 避免 Newtonsoft.Json 直接序列化导致 PascalCase 输出
        return Ok(result);
    }
}
