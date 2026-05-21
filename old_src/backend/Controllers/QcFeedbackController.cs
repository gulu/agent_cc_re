// 质控反馈 API
// 路由：/api/v1/qc/feedback
// 医生对质控结果的确认/修正，数据积累后用于模型迭代训练

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Entities;
using ReportQC.Models;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/qc/feedback")]
public class QcFeedbackController : BaseController
{
    private readonly IFreeSql _fsql;

    public QcFeedbackController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>
    /// 提交质控反馈（医生确认/修正）
    /// </summary>
    [HttpPost]
    public IActionResult Submit([FromBody] QcFeedbackRequest req)
    {
        try
        {
            var feedback = new QcFeedback
            {
                QcReportId = req.QcReportId,
                IssueId = req.IssueId,
                Action = req.Action,
                Comment = req.Comment,
                CorrectedText = req.CorrectedText,
                SupplementIssues = req.SupplementIssues,
                DoctorName = req.DoctorName,
                CreatedAt = DateTime.Now
            };

            _fsql.Insert(feedback).ExecuteAffrows();

            // 同时更新 qc_issue 的 is_accepted 字段
            if (req.IssueId > 0)
            {
                _fsql.Update<QcIssue>()
                    .Set(i => i.IsAccepted, req.Action == "accepted")
                    .Where(i => i.Id == req.IssueId)
                    .ExecuteAffrows();
            }

            JSBaseLogs.QcLog($"质控反馈提交：报告#{req.QcReportId} 问题#{req.IssueId} → {req.Action}",
                req.QcReportId.ToString());

            return Success(feedback, "反馈提交成功");
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return Error($"提交反馈失败：{ex.Message}");
        }
    }

    /// <summary>查询质控反馈列表</summary>
    [HttpGet]
    public IActionResult GetList([FromQuery] long? qcReportId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _fsql.Select<QcFeedback>().OrderByDescending(f => f.Id);
            if (qcReportId.HasValue)
                query = query.Where(f => f.QcReportId == qcReportId.Value);

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Limit(pageSize).ToList();

            return Success(new { items, total, page, pageSize });
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return Error($"查询失败：{ex.Message}");
        }
    }

    /// <summary>质控反馈统计（用于模型训练评估）</summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        try
        {
            var stats = new
            {
                totalFeedback = _fsql.Select<QcFeedback>().Count(),
                byAction = new
                {
                    accepted = _fsql.Select<QcFeedback>().Where(f => f.Action == "accepted").Count(),
                    rejected = _fsql.Select<QcFeedback>().Where(f => f.Action == "rejected").Count(),
                    modified = _fsql.Select<QcFeedback>().Where(f => f.Action == "modified").Count(),
                    supplement = _fsql.Select<QcFeedback>().Where(f => f.Action == "supplement").Count(),
                },
                // 质控准确率 = 被接受的 / 有反馈的
                totalReviewed = _fsql.Select<QcFeedback>().Count(),
            };

            return Success(stats);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return Error($"查询统计失败：{ex.Message}");
        }
    }
}
