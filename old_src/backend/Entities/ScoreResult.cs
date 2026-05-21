// 评分结果明细表 ScoreResult — 对应 score_result 表
// 每份质控报告在各维度的得分明细

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class ScoreResult : JSBaseDBEntity
{
    public long QcReportId { get; set; }
    public long DimensionId { get; set; }
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
    public string? DeductionItems { get; set; } // JSON
    public DateTime CreatedAt { get; set; }

    [Navigate(nameof(QcReportId))]
    public QcReport? QcReport { get; set; }

    [Navigate(nameof(DimensionId))]
    public ScoreDimension? Dimension { get; set; }
}
