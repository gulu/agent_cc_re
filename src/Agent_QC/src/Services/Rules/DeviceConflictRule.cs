using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 检查设备-描述矛盾。检查报告中出现的影像设备描述是否与申请检查设备一致。
/// 如CT检查不应出现"MRI示"、MRI检查不应出现"CT示"等。
/// </summary>
public class DeviceConflictRule
{
    /// <summary>各检查设备的禁止描述词。</summary>
    private static readonly Dictionary<string, string[]> DeviceBannedTerms = new()
    {
        ["CT"] = new[] { "MRI示", "MR示", "磁共振示", "MRI平扫", "MRI增强", "T1WI", "T2WI", "DWI", "FLAIR" },
        ["MRI"] = new[] { "CT示", "CT平扫", "CT增强", "CT扫描", "X线片", "X光片", "CTA示" },
        ["DR"] = new[] { "CT示", "MRI示", "MR示", "磁共振示", "CT值", "CT增强" },
        ["超声"] = new[] { "CT示", "MRI示", "MR示", "X线片", "CT值", "CT增强" },
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExamDevice))
            return new List<QcIssueDto>();

        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (var (device, bannedTerms) in DeviceBannedTerms)
        {
            // 设备名称可能不完全匹配，如"头颅CT"包含"CT"
            if (!request.ExamDevice.Contains(device, StringComparison.Ordinal))
                continue;

            foreach (var term in bannedTerms)
            {
                if (fullText.Contains(term, StringComparison.Ordinal))
                {
                    issues.Add(new QcIssueDto
                    {
                        IssueType = "device_conflict",
                        Severity = "error",
                        Description = $"检查设备为{request.ExamDevice}，但报告中出现「{term}」",
                    });
                }
            }
        }

        return issues;
    }
}
