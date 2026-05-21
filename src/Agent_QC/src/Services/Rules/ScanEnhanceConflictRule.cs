using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 检查扫描方式-增强描述矛盾。
/// 平扫检查不应出现"强化"、"增强"等描述词。
/// </summary>
public class ScanEnhanceConflictRule
{
    private static readonly string[] EnhanceDescriptions =
    {
        "强化", "增强", "明显强化", "明显增强", "中度强化", "轻度强化",
        "不均匀强化", "环形强化", "进行性强化", "延迟强化", "渐进性强化",
        "快进快出", "快进慢出", "动脉期强化", "门脉期强化", "延迟期强化",
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExamMethod))
            return new List<QcIssueDto>();

        // 非平扫（增强/CTA等）不需要检查
        if (!request.ExamMethod.Contains("平扫", StringComparison.Ordinal))
            return new List<QcIssueDto>();

        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (var desc in EnhanceDescriptions)
        {
            if (fullText.Contains(desc, StringComparison.Ordinal))
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "scan_enhance_conflict",
                    Severity = "error",
                    Description = $"检查方式为平扫，但报告中出现增强描述「{desc}」",
                });
                break; // 只报第一个，避免重复
            }
        }

        return issues;
    }
}
