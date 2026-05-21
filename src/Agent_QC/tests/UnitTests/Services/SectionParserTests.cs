using Xunit;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class SectionParserTests
{
    private readonly SectionParser _parser = new();

    [Fact]
    public void 解析影像所见段落()
    {
        var text = "影像所见：双肺纹理清晰，未见明确结节影。\n诊断意见：未见明显异常。";
        var sections = _parser.Parse(text);

        Assert.Contains("双肺纹理清晰", sections.Findings);
    }

    [Fact]
    public void 解析诊断意见段落()
    {
        var text = "影像所见：双肺纹理清晰。\n诊断意见：未见明显异常。";
        var sections = _parser.Parse(text);

        Assert.Contains("未见明显异常", sections.Impression);
    }

    [Fact]
    public void 解析临床诊断段落()
    {
        var text = "临床诊断：咳嗽待查。\n影像所见：双肺清晰。\n诊断意见：正常。";
        var sections = _parser.Parse(text);

        Assert.Contains("咳嗽待查", sections.ClinicalHistory);
    }

    [Fact]
    public void 解析检查所见变体()
    {
        var text = "检查所见：右肺下叶结节。\n诊断结论：右肺下叶炎性结节可能。";
        var sections = _parser.Parse(text);

        Assert.Contains("右肺下叶结节", sections.Findings);
        Assert.Contains("炎性结节可能", sections.Impression);
    }

    [Fact]
    public void 解析建议段落()
    {
        var text = "诊断意见：右肺结节。\n建议：3个月后复查CT。";
        var sections = _parser.Parse(text);

        Assert.Contains("3个月后复查CT", sections.Recommendation);
    }

    [Fact]
    public void 无明确段落头时_全文作为Findings()
    {
        var text = "双肺纹理清晰，未见明确异常。";
        var sections = _parser.Parse(text);

        Assert.Contains("双肺纹理清晰", sections.Findings);
    }

    [Fact]
    public void 多段落混合解析()
    {
        var text = "临床诊断：发热待查。\n影像所见：双肺纹理增粗。\n诊断意见：支气管炎可能。\n建议：短期随访。";
        var sections = _parser.Parse(text);

        Assert.Contains("发热待查", sections.ClinicalHistory);
        Assert.Contains("双肺纹理增粗", sections.Findings);
        Assert.Contains("支气管炎可能", sections.Impression);
        Assert.Contains("短期随访", sections.Recommendation);
    }

    [Fact]
    public void 以冒号开头的段落()
    {
        var text = "影像表现：右肺中叶见磨玻璃密度影。\n印象：右肺中叶磨玻璃结节，建议6个月随访。";
        var sections = _parser.Parse(text);

        Assert.Contains("磨玻璃密度影", sections.Findings);
        Assert.Contains("磨玻璃结节", sections.Impression);
    }

    [Fact]
    public void 影像学表现变体()
    {
        var text = "影像学表现：双肺野清晰。\n诊断：正常胸部CT。";
        var sections = _parser.Parse(text);

        Assert.Contains("双肺野清晰", sections.Findings);
    }

    [Fact]
    public void 空字符串返回空段落()
    {
        var sections = _parser.Parse("");
        Assert.Equal("", sections.Findings);
        Assert.Equal("", sections.Impression);
    }
}
