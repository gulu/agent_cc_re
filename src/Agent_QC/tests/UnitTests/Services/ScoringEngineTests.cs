using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    [Fact]
    public void 无问题_满分100()
    {
        var response = new QcResponse { ReportId = "R1" };
        var result = _engine.Calculate(response);

        Assert.Equal(100m, result.TotalScore);
        Assert.True(result.Passed);
        Assert.Equal(4, result.CheckItems.Count);
        Assert.All(result.CheckItems, c => Assert.Equal(100m, c.Score));
    }

    [Fact]
    public void 一个error_扣10分()
    {
        var response = new QcResponse
        {
            ReportId = "R1",
            Issues = new List<QcIssueDto>
            {
                new() { Severity = "error", IssueType = "text_error" },
            },
        };
        var result = _engine.Calculate(response);

        // error→normative维度，weight 30%，扣10分 = 90分
        Assert.True(result.TotalScore < 100m);
        Assert.True(result.TotalScore > 90m);
    }

    [Fact]
    public void 危急值_扣20分_维度扣除()
    {
        var response = new QcResponse
        {
            ReportId = "R1",
            Issues = new List<QcIssueDto>
            {
                new() { Severity = "critical", IssueType = "critical_sign" },
            },
        };
        var result = _engine.Calculate(response);

        // timeliness维度(15%): 100-20=80, 总分=100*0.3+100*0.25+100*0.3+80*0.15=97
        Assert.Equal(97m, result.TotalScore);
        Assert.True(result.Passed);
        // timeliness 维度分确实降低了
        var timeliness = result.CheckItems.Find(c => c.DimensionCode == "timeliness");
        Assert.Equal(80m, timeliness!.Score);
    }

    [Fact]
    public void 混合问题_累加扣分()
    {
        var response = new QcResponse
        {
            ReportId = "R1",
            Issues = new List<QcIssueDto>
            {
                new() { Severity = "error", IssueType = "text_error" },
                new() { Severity = "warning", IssueType = "colloquial" },
                new() { Severity = "critical", IssueType = "critical_sign" },
            },
        };
        var result = _engine.Calculate(response);

        // text_error→normative(30%): -10→90
        // colloquial→normative(30%): -5→85
        // critical_sign→timeliness(15%): -20→80
        // total = 90*0.3 + 100*0.25 + 100*0.3 + 80*0.15 = 97... actually let me check
        // Actually: normative: 100-10-5=85, completeness:100, logic:100, timeliness:100-20=80
        // total = 85*0.3 + 100*0.25 + 100*0.3 + 80*0.15 = 25.5 + 25 + 30 + 12 = 92.5
        Assert.Equal(92.5m, result.TotalScore);
        Assert.True(result.Passed);
    }
}
