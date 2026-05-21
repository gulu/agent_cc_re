using Moq;
using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class QaArbiterTests
{
    private readonly QaArbiter _arbiter = new();

    [Fact]
    public async Task 无Skill结果_保持原issues不变()
    {
        var issues = new List<QcIssueDto> { new() { IssueType = "gender_conflict", Severity = "error" } };

        var result = await _arbiter.ArbitrateAsync(issues, new List<SkillResult>());

        Assert.Single(result);
        Assert.Equal("error", result[0].Severity);
    }

    [Fact]
    public async Task Skill高置信度pass_移除规则error()
    {
        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict", Severity = "error", Description = "性别矛盾" }
        };
        var skills = new List<SkillResult>
        {
            new() { SkillId = "gender-anatomy-checker", Judgment = "pass", Confidence = 0.92f }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        Assert.Empty(result); // LLM 推翻规则
    }

    [Fact]
    public async Task Skill高置信度fail_确认规则error()
    {
        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict", Severity = "error", Description = "性别矛盾" }
        };
        var skills = new List<SkillResult>
        {
            new() { SkillId = "gender-anatomy-checker", Judgment = "fail", Confidence = 0.88f }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        Assert.Single(result);
        Assert.Equal("error", result[0].Severity);
        Assert.Contains("LLM确认", result[0].Description);
    }

    [Fact]
    public async Task Skill低置信度pass_降级为warning()
    {
        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict", Severity = "error", Description = "性别矛盾" }
        };
        var skills = new List<SkillResult>
        {
            new() { SkillId = "gender-anatomy-checker", Judgment = "pass", Confidence = 0.65f }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        Assert.Single(result);
        Assert.Equal("warning", result[0].Severity);
        Assert.Contains("LLM不确定", result[0].Description);
    }

    [Fact]
    public async Task Skill发现新问题_高置信度_新增error()
    {
        var issues = new List<QcIssueDto>();
        var skills = new List<SkillResult>
        {
            new() { SkillId = "critical-sign-arbiter", Judgment = "fail", Confidence = 0.9f,
                Reason = "报告提及主动脉夹层", Suggestion = "建议立即通知临床医生" }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        Assert.Single(result);
        Assert.Equal("error", result[0].Severity);
        Assert.Equal("critical_sign", result[0].IssueType);
        Assert.Contains("LLM发现", result[0].Description);
    }

    [Fact]
    public async Task Skill发现新问题_中置信度_新增warning()
    {
        var issues = new List<QcIssueDto>();
        var skills = new List<SkillResult>
        {
            new() { SkillId = "terminology-validator", Judgment = "fail", Confidence = 0.75f,
                Reason = "存在口语化表述", Suggestion = "请使用规范医学用语" }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        Assert.Single(result);
        Assert.Equal("warning", result[0].Severity);
        Assert.Equal("text_error", result[0].IssueType);
    }

    [Fact]
    public async Task Skill置信度极低_忽略()
    {
        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict", Severity = "error" }
        };
        var skills = new List<SkillResult>
        {
            new() { SkillId = "gender-anatomy-checker", Judgment = "fail", Confidence = 0.4f }
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        // 忽略低置信度 Skill，保持原规则判定
        Assert.Single(result);
        Assert.Equal("error", result[0].Severity);
        Assert.Equal("gender_conflict", result[0].IssueType);
    }

    [Fact]
    public async Task 多个规则和多个Skill_正确匹配和消解()
    {
        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict", Severity = "error", Description = "性别矛盾" },
            new() { IssueType = "device_conflict", Severity = "error", Description = "设备矛盾" },
        };
        var skills = new List<SkillResult>
        {
            new() { SkillId = "gender-anatomy-checker", Judgment = "pass", Confidence = 0.93f }, // 推翻
            new() { SkillId = "device-method-validator", Judgment = "fail", Confidence = 0.85f }, // 确认
        };

        var result = await _arbiter.ArbitrateAsync(issues, skills);

        // gender 被移除，device 被保留+确认
        Assert.Single(result);
        Assert.Equal("device_conflict", result[0].IssueType);
        Assert.Contains("LLM确认", result[0].Description);
    }
}
