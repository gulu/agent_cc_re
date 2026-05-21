using Moq;
using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class HermesOrchestratorTests
{
    private static Mock<IVllmClient> CreateHealthyVllm()
    {
        var mock = new Mock<IVllmClient>();
        mock.Setup(v => v.Health).Returns(VllmHealthStatus.Healthy);
        return mock;
    }

    [Fact]
    public async Task 空规则列表_不触发任何Skill()
    {
        var vllm = CreateHealthyVllm();
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var results = await orchestrator.DispatchAsync(
            new QcRequest { Findings = "正常。", Impression = "正常。" },
            new List<QcIssueDto>(),
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task vLLM不可用_返回空列表()
    {
        var vllm = new Mock<IVllmClient>();
        vllm.Setup(v => v.Health).Returns(VllmHealthStatus.Unavailable);
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict" },
        };
        var results = await orchestrator.DispatchAsync(new QcRequest(), issues, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task gender_conflict触发gender_anatomy_checker()
    {
        var vllm = CreateHealthyVllm();
        vllm.Setup(v => v.ChatAsync(It.IsAny<VllmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VllmChatRequest req, CancellationToken _) => new VllmChatResponse
            {
                Choices = new List<VllmChoice>
                {
                    new() { Message = new VllmMessage { Content = "{\"judgment\":\"pass\",\"confidence\":0.95}" } }
                }
            });
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var issues = new List<QcIssueDto> { new() { IssueType = "gender_conflict" } };
        var results = await orchestrator.DispatchAsync(
            new QcRequest { PatientGender = "男", Findings = "子宫肌瘤。" }, issues, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("gender-anatomy-checker", results[0].SkillId);
    }

    [Fact]
    public async Task 多项规则_触发多个Skill并行调用()
    {
        var vllm = CreateHealthyVllm();
        vllm.Setup(v => v.ChatAsync(It.IsAny<VllmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VllmChatRequest req, CancellationToken _) => new VllmChatResponse
            {
                Choices = new List<VllmChoice>
                {
                    new() { Message = new VllmMessage { Content = "{\"judgment\":\"pass\",\"confidence\":0.9}" } }
                }
            });
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var issues = new List<QcIssueDto>
        {
            new() { IssueType = "gender_conflict" },
            new() { IssueType = "critical_sign" },
            new() { IssueType = "device_conflict" },
            new() { IssueType = "completeness_error" },
        };
        var results = await orchestrator.DispatchAsync(new QcRequest(), issues, CancellationToken.None);

        Assert.Equal(4, results.Count);
        Assert.Contains(results, r => r.SkillId == "gender-anatomy-checker");
        Assert.Contains(results, r => r.SkillId == "critical-sign-arbiter");
        Assert.Contains(results, r => r.SkillId == "device-method-validator");
        Assert.Contains(results, r => r.SkillId == "measurement-completeness");
    }

    [Fact]
    public async Task 未知IssueType_不触发Skill()
    {
        var vllm = CreateHealthyVllm();
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var issues = new List<QcIssueDto> { new() { IssueType = "some_unknown_type" } };
        var results = await orchestrator.DispatchAsync(new QcRequest(), issues, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task vllm返回null_过滤掉null结果()
    {
        var vllm = CreateHealthyVllm();
        vllm.Setup(v => v.ChatAsync(It.IsAny<VllmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VllmChatResponse?)null);
        var registry = new SkillRegistry("knowledge/skills");
        var orchestrator = new HermesOrchestrator(vllm.Object, registry);

        var issues = new List<QcIssueDto> { new() { IssueType = "gender_conflict" } };
        var results = await orchestrator.DispatchAsync(new QcRequest(), issues, CancellationToken.None);

        Assert.Empty(results);
    }
}
