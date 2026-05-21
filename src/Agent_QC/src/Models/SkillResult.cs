namespace Agent_QC.Models;

public class SkillResult
{
    public string SkillId { get; set; } = "";
    public string Judgment { get; set; } = "";    // "pass" | "fail" | "uncertain"
    public float Confidence { get; set; }
    public string Reason { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public bool IsJsonValid { get; set; }

    public static SkillResult? FromJson(string skillId, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new SkillResult
            {
                SkillId = skillId,
                Judgment = root.TryGetProperty("judgment", out var j) ? j.GetString() ?? "" : "",
                Confidence = root.TryGetProperty("confidence", out var c) ? c.GetSingle() : 0f,
                Reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                Suggestion = root.TryGetProperty("suggestion", out var s) ? s.GetString() ?? "" : "",
                IsJsonValid = true,
            };
        }
        catch
        {
            return new SkillResult { SkillId = skillId, IsJsonValid = false, Reason = json };
        }
    }
}
