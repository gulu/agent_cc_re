// 质控记录查询服务
// 历史质控结果查询、统计

using FreeSql;
using ReportQC.Entities;
using ReportQC.Models;

namespace ReportQC.Services;

public static class QcQueryService
{
    /// <summary>质控记录列表（分页+筛选）</summary>
    public static AjaxResult GetReportList(IFreeSql fsql, string? reportId = null,
        string? reportType = null, bool? passed = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        int page = 1, int pageSize = 20)
    {
        try
        {
            var query = fsql.Select<QcReport>().OrderByDescending(r => r.Id);

            if (!string.IsNullOrEmpty(reportId))
                query = query.Where(r => r.ReportId.Contains(reportId));
            if (!string.IsNullOrEmpty(reportType))
                query = query.Where(r => r.ReportType == reportType);
            if (passed.HasValue)
                query = query.Where(r => r.Passed == passed.Value);
            if (dateFrom.HasValue)
                query = query.Where(r => r.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(r => r.CreatedAt <= dateTo.Value);

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Limit(pageSize).ToList();

            return AjaxResult.Success(new { items, total, page, pageSize });
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }

    /// <summary>质控记录详情（含问题明细 + 评分 + 反馈）</summary>
    public static AjaxResult GetReportDetail(IFreeSql fsql, long id)
    {
        try
        {
            var report = fsql.Select<QcReport>().Where(r => r.Id == id).First();
            if (report == null)
                return AjaxResult.Error("记录不存在");

            var issues = fsql.Select<QcIssue>().Where(i => i.QcReportId == id).ToList();
            var scores = fsql.Select<ScoreResult>().Where(s => s.QcReportId == id).ToList();
            var feedbacks = fsql.Select<QcFeedback>().Where(f => f.QcReportId == id).ToList();

            return AjaxResult.Success(new { report, issues, scores, feedbacks });
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }

    /// <summary>质控统计概览</summary>
    public static AjaxResult GetStats(IFreeSql fsql)
    {
        try
        {
            var today = DateTime.Today;

            var stats = new
            {
                totalReports = fsql.Select<QcReport>().Count(),
                todayReports = fsql.Select<QcReport>().Where(r => r.CreatedAt >= today).Count(),
                passed = fsql.Select<QcReport>().Where(r => r.Passed == true).Count(),
                failed = fsql.Select<QcReport>().Where(r => r.Passed == false).Count(),
                avgScore = fsql.Select<QcReport>().Where(r => r.TotalScore != null).ToList(r => r.TotalScore!.Value)
                    .DefaultIfEmpty().Average(),
                totalIssues = fsql.Select<QcIssue>().Count(),
                totalFeedback = fsql.Select<QcFeedback>().Count(),
                acceptedFeedback = fsql.Select<QcFeedback>().Where(f => f.Action == "accepted").Count(),
                rejectedFeedback = fsql.Select<QcFeedback>().Where(f => f.Action == "rejected").Count(),
                byType = fsql.Select<QcReport>().ToList(r => r.ReportType)
                    .GroupBy(t => t ?? "未知")
                    .Select(g => new { type = g.Key, count = g.Count() }).ToList(),
                byLevel = fsql.Select<QcReport>().ToList(r => r.QcLevel)
                    .GroupBy(l => l ?? "未知")
                    .Select(g => new { level = g.Key, count = g.Count() }).ToList(),
            };

            return AjaxResult.Success(stats);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"统计失败：{ex.Message}");
        }
    }
}
