using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 方位-左右矛盾检测。
/// 检查影像所见中描述的方位是否与诊断结论一致。
/// 如所见描述"左侧"，结论写"右侧"则为矛盾。
/// </summary>
public class DirectionConflictRule
{
    private static readonly string[] LeftPositive = { "左侧", "左叶", "左侧壁", "左肺", "左肾", "左上", "左下", "左前", "左后" };
    private static readonly string[] RightPositive = { "右侧", "右叶", "右侧壁", "右肺", "右肾", "右上", "右下", "右前", "右后" };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var findings = request.Findings ?? "";
        var impression = request.Impression ?? "";
        var issues = new List<QcIssueDto>();

        // 仅当 findings 和 impression 都出现方位词时才检查
        bool findingsLeft = LeftPositive.Any(f => findings.Contains(f, StringComparison.Ordinal));
        bool findingsRight = RightPositive.Any(f => findings.Contains(f, StringComparison.Ordinal));
        bool impressionLeft = LeftPositive.Any(f => impression.Contains(f, StringComparison.Ordinal));
        bool impressionRight = RightPositive.Any(f => impression.Contains(f, StringComparison.Ordinal));

        if (findingsLeft && impressionRight && !impressionLeft)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "direction_conflict",
                Severity = "warning",
                Description = "影像所见为左侧，诊断结论为右侧，请确认方位是否准确",
            });
        }

        if (findingsRight && impressionLeft && !impressionRight)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "direction_conflict",
                Severity = "warning",
                Description = "影像所见为右侧，诊断结论为左侧，请确认方位是否准确",
            });
        }

        return issues;
    }
}
