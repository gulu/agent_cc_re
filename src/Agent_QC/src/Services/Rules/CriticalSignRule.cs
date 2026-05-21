using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 危急征象检测。检查报告中是否出现需要紧急处理的危急征象关键词，
/// 如主动脉夹层、脑出血、急性心梗等。
/// </summary>
public class CriticalSignRule
{
    private readonly NegationDetector _negationDetector = new();

    private static readonly string[] CriticalSigns =
    {
        "主动脉夹层", "主动脉破裂", "主动脉壁间血肿",
        "脑出血", "颅内出血", "硬膜外血肿", "硬膜下血肿", "蛛网膜下腔出血",
        "脑疝", "脑疝形成",
        "急性肺栓塞", "大面积肺栓塞",
        "急性心梗", "心肌梗死", "急性冠状动脉综合征",
        "肝破裂", "脾破裂", "肾破裂", "腹腔积血",
        "宫外孕", "异位妊娠",
        "消化道穿孔", "肠穿孔", "胃穿孔",
        "急性胰腺炎重症", "坏死性胰腺炎",
        "急性化脓性胆管炎",
        "肠系膜缺血", "肠系膜动脉栓塞",
        "心包积液伴填塞", "大量心包积液",
        "活动性出血",
        "脊髓压迫", "急性脊髓压迫症",
        "颈动脉夹层", "椎动脉夹层",
        "颅内动脉瘤破裂",
        "急性肾损伤", "急性肾功能衰竭",
        "上腔静脉综合征",
        "睾丸扭转",
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (var sign in CriticalSigns)
        {
            if (!fullText.Contains(sign, StringComparison.Ordinal)) continue;

            // 否定语境跳过：如"未见明确脑出血"
            if (_negationDetector.IsNegated(fullText, sign)) continue;

            issues.Add(new QcIssueDto
            {
                IssueType = "critical_sign",
                Severity = "critical",
                Description = $"报告出现危急征象：「{sign}」，请立即处理",
            });
        }

        return issues;
    }
}
