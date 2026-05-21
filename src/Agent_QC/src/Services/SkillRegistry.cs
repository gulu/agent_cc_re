using Agent_QC.Models;

namespace Agent_QC.Services;

public class SkillRegistry
{
    private readonly Dictionary<string, (string System, string User)> _prompts = new();

    public IReadOnlyList<string> SkillIds => _prompts.Keys.ToList().AsReadOnly();

    public SkillRegistry(string skillDir = "knowledge/skills")
    {
        var resolved = ResolveSkillDir(skillDir);
        if (resolved == null)
            return;

        foreach (var file in Directory.GetFiles(resolved, "*.md"))
        {
            var skillId = Path.GetFileNameWithoutExtension(file);
            var (system, user) = ParsePromptFile(file);
            _prompts[skillId] = (system, user);
        }
    }

    private static string? ResolveSkillDir(string skillDir)
    {
        if (Path.IsPathRooted(skillDir) && Directory.Exists(skillDir))
            return skillDir;

        // Try relative to base directory
        var fromBase = Path.Combine(AppContext.BaseDirectory, skillDir);
        if (Directory.Exists(fromBase))
            return fromBase;

        // Walk up from base directory to find knowledge/skills
        var current = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, skillDir);
            if (Directory.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return null;
    }

    private static (string system, string user) ParsePromptFile(string path)
    {
        var text = File.ReadAllText(path);
        var system = ExtractSection(text, "## System");
        var user = ExtractSection(text, "## User");
        return (system, user);
    }

    private static string ExtractSection(string text, string header)
    {
        var startIdx = text.IndexOf(header, StringComparison.Ordinal);
        if (startIdx < 0) return "";

        startIdx += header.Length;
        var endIdx = text.IndexOf("\n## ", startIdx, StringComparison.Ordinal);
        if (endIdx < 0) endIdx = text.Length;

        return text[startIdx..endIdx].Trim();
    }

    public bool HasSkill(string skillId) => _prompts.ContainsKey(skillId);

    public string BuildSystemPrompt(string skillId)
    {
        return _prompts.TryGetValue(skillId, out var p) ? p.System : "";
    }

    public string BuildUserPrompt(string skillId, QcRequest request)
    {
        if (!_prompts.TryGetValue(skillId, out var p))
            return "";

        return p.User
            .Replace("{PatientGender}", request.PatientGender ?? "")
            .Replace("{PatientAge}", request.PatientAge?.ToString() ?? "")
            .Replace("{Findings}", request.Findings ?? "")
            .Replace("{Impression}", request.Impression ?? "")
            .Replace("{ExamPart}", request.ExamPart ?? "")
            .Replace("{ExamDevice}", request.ExamDevice ?? "")
            .Replace("{ExamMethod}", request.ExamMethod ?? "")
            .Replace("{ReportType}", request.ReportType ?? "")
            .Replace("{ClinicalDiagnosis}", request.ClinicalDiagnosis ?? "");
    }
}
