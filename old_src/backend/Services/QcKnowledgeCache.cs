// QcKnowledgeCache — 知识库内存缓存
// 应用启动时从 knowledge_base 表加载到静态 Dictionary
// 运行时 O(1) 查表，零 IO 开销

using FreeSql;
using Newtonsoft.Json;
using ReportQC.Entities;

namespace ReportQC.Services;

public static class QcKnowledgeCache
{
    // ── 规则引擎字典 ──────────────────────────────

    public static Dictionary<string, List<string>> GenderExcludeFemale { get; private set; } = new();
    public static Dictionary<string, List<string>> GenderExcludeMale { get; private set; } = new();
    public static Dictionary<string, List<string>> BodyPartAreaMap { get; private set; } = new();
    public static Dictionary<string, List<string>> AgeExclude { get; private set; } = new();
    public static Dictionary<string, List<string>> ExamDeviceKeywords { get; private set; } = new();
    public static Dictionary<string, List<string>> ScanTypeMap { get; private set; } = new();
    public static List<string> CriticalSignKeywords { get; private set; } = new();
    public static List<string> DirectionPositive { get; private set; } = new();
    public static List<string> DirectionNegative { get; private set; } = new();
    public static string? UnitPattern { get; private set; }
    public static Dictionary<string, List<string>> RadsRequireTypes { get; private set; } = new();

    // ── 新增字典 — 格式规范化 / 完整性 / 随访 ──────

    /// <summary>患者基本信息必填字段</summary>
    public static List<string> RequiredPatientFields { get; private set; } = new();

    /// <summary>检查技术必填描述项</summary>
    public static List<string> RequiredExamTechFields { get; private set; } = new();

    /// <summary>需要随访建议的诊断关键词</summary>
    public static List<string> FollowupIndicators { get; private set; } = new();

    /// <summary>阳性诊断关键词（结论中出现即认为给出诊断）</summary>
    public static List<string> PositiveDiagnosisWords { get; private set; } = new();

    // ── 术语标准 ──────────────────────────────────

    public static List<TerminologyStandard> TerminologyList { get; private set; } = new();
    public static bool Loaded { get; private set; }

    /// <summary>从数据库加载知识库到内存</summary>
    public static void Load(IFreeSql fsql)
    {
        var all = fsql.Select<KnowledgeBase>()
            .Where(k => k.IsActive)
            .ToList();

        GenderExcludeFemale   = BuildMap(all, "gender_exclude_female");
        GenderExcludeMale     = BuildMap(all, "gender_exclude_male");
        BodyPartAreaMap       = BuildMap(all, "body_part_area_map");
        AgeExclude            = BuildMap(all, "age_exclude");
        ExamDeviceKeywords    = BuildMap(all, "exam_device_keywords");
        ScanTypeMap           = BuildMap(all, "scan_type_map");

        CriticalSignKeywords  = BuildList(all, "critical_sign_keywords");
        DirectionPositive     = BuildList(all, "direction_positive");
        DirectionNegative     = BuildList(all, "direction_negative");

        UnitPattern = all.FirstOrDefault(k => k.CategoryCode == "unit_pattern")?.MatchValue;

        RadsRequireTypes = BuildMap(all, "rads_require_types");

        // 新增字典
        RequiredPatientFields  = BuildList(all, "required_fields");
        RequiredExamTechFields = BuildList(all, "exam_technique_required");
        FollowupIndicators     = BuildList(all, "followup_indicators");
        PositiveDiagnosisWords = BuildList(all, "positive_diagnosis_words");

        TerminologyList = fsql.Select<TerminologyStandard>()
            .Where(t => t.IsActive)
            .ToList();

        Loaded = true;
    }

    public static void Reload(IFreeSql fsql) => Load(fsql);

    private static Dictionary<string, List<string>> BuildMap(List<KnowledgeBase> all, string category)
    {
        return all
            .Where(k => k.CategoryCode == category)
            .ToDictionary(
                k => k.MatchKey,
                k =>
                {
                    try { return JsonConvert.DeserializeObject<List<string>>(k.MatchValue) ?? new(); }
                    catch { return new List<string>(); }
                }
            );
    }

    private static List<string> BuildList(List<KnowledgeBase> all, string category)
    {
        var entry = all.FirstOrDefault(k => k.CategoryCode == category);
        if (entry == null) return new();
        try { return JsonConvert.DeserializeObject<List<string>>(entry.MatchValue) ?? new(); }
        catch { return new(); }
    }
}
