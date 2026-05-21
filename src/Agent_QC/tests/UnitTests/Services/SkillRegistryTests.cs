using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class SkillRegistryTests
{
    [Fact]
    public void 加载所有Skill模板()
    {
        var registry = new SkillRegistry("knowledge/skills");

        Assert.Contains("gender-anatomy-checker", registry.SkillIds);
        Assert.Contains("findings-impression-nli", registry.SkillIds);
        Assert.Contains("critical-sign-arbiter", registry.SkillIds);
        Assert.Contains("site-consistency-checker", registry.SkillIds);
        Assert.Contains("device-method-validator", registry.SkillIds);
        Assert.Contains("measurement-completeness", registry.SkillIds);
        Assert.Contains("rads-compliance-checker", registry.SkillIds);
        Assert.Contains("terminology-validator", registry.SkillIds);
        Assert.Equal(8, registry.SkillIds.Count);
    }

    [Fact]
    public void 构建SystemPrompt()
    {
        var registry = new SkillRegistry("knowledge/skills");
        var prompt = registry.BuildSystemPrompt("gender-anatomy-checker");

        Assert.Contains("解剖-性别审查专家", prompt);
        Assert.Contains("judgment", prompt);
        Assert.Contains("confidence", prompt);
        Assert.DoesNotContain("{Findings}", prompt);  // system prompt不含占位符
    }

    [Fact]
    public void 构建UserPrompt_替换占位符()
    {
        var registry = new SkillRegistry("knowledge/skills");
        var request = new QcRequest
        {
            PatientGender = "男",
            Findings = "子宫肌瘤约3cm。",
            Impression = "子宫肌瘤。",
            ExamPart = "盆腔",
            ExamDevice = "CT",
            ExamMethod = "平扫",
        };

        var prompt = registry.BuildUserPrompt("gender-anatomy-checker", request);

        Assert.Contains("男", prompt);
        Assert.Contains("子宫肌瘤约3cm", prompt);
        Assert.Contains("子宫肌瘤", prompt);
        Assert.DoesNotContain("{PatientGender}", prompt);
        Assert.DoesNotContain("{Findings}", prompt);
    }

    [Fact]
    public void 不存在的Skill返回空()
    {
        var registry = new SkillRegistry("knowledge/skills");

        Assert.False(registry.HasSkill("nonexistent"));
        Assert.Equal("", registry.BuildSystemPrompt("nonexistent"));
        Assert.Equal("", registry.BuildUserPrompt("nonexistent", new QcRequest()));
    }

    [Fact]
    public void SkillResult解析有效JSON()
    {
        var json = "{\"judgment\":\"fail\",\"confidence\":0.92,\"reason\":\"男性患者出现子宫\",\"suggestion\":\"请核实患者性别\"}";
        var result = SkillResult.FromJson("gender-anatomy-checker", json);

        Assert.NotNull(result);
        Assert.True(result!.IsJsonValid);
        Assert.Equal("fail", result.Judgment);
        Assert.Equal(0.92f, result.Confidence);
        Assert.Contains("男性", result.Reason);
    }

    [Fact]
    public void SkillResult解析无效JSON_标记无效()
    {
        var result = SkillResult.FromJson("test-skill", "这不是JSON");

        Assert.NotNull(result);
        Assert.False(result!.IsJsonValid);
        Assert.Equal("这不是JSON", result.Reason);
    }

    [Fact]
    public void SkillResult解析null返回null()
    {
        var result = SkillResult.FromJson("test-skill", null);
        Assert.Null(result);
    }
}
