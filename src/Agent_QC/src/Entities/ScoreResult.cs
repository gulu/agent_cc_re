using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

public class ScoreResult : EntityBase
{
    public long QcReportId { get; set; }
    public long DimensionId { get; set; }
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
    public string? DeductionItems { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(nameof(QcReportId))]
    public QcReport? QcReport { get; set; }

    [Navigate(nameof(DimensionId))]
    public ScoreDimension? Dimension { get; set; }
}
