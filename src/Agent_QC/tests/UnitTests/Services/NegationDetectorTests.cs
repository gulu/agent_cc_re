using Xunit;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class NegationDetectorTests
{
    private readonly NegationDetector _detector = new();

    [Fact]
    public void 未见明确结节影_结节被否定()
    {
        var result = _detector.IsNegated("未见明确结节影", "结节");
        Assert.True(result);
    }

    [Fact]
    public void 未见异常但建议随访_随访不被否定()
    {
        var result = _detector.IsNegated("未见异常，但建议随访", "随访");
        Assert.False(result);
    }

    [Fact]
    public void 无明显强化_强化被否定()
    {
        var result = _detector.IsNegated("无明显强化", "强化");
        Assert.True(result);
    }

    [Fact]
    public void 子宫未见显示_子宫被否定()
    {
        var result = _detector.IsNegated("子宫未见显示", "子宫");
        Assert.False(result, "子宫在否定词之前，不应被否定");
    }

    [Fact]
    public void 未见子宫及附件异常_子宫被否定()
    {
        var result = _detector.IsNegated("未见子宫及附件异常", "子宫");
        Assert.True(result);
    }

    [Fact]
    public void 未见子宫及附件异常_附件被否定()
    {
        var result = _detector.IsNegated("未见子宫及附件异常", "附件");
        Assert.True(result);
    }

    [Fact]
    public void 子宫肌瘤_未被否定()
    {
        var result = _detector.IsNegated("子宫肌瘤", "子宫");
        Assert.False(result);
    }

    [Fact]
    public void 前列腺未见占位_占位被否定()
    {
        var result = _detector.IsNegated("前列腺未见占位，膀胱正常", "占位");
        Assert.True(result);
    }

    [Fact]
    public void 前列腺未见占位_膀胱不被否定()
    {
        var result = _detector.IsNegated("前列腺未见占位，膀胱正常", "膀胱");
        Assert.False(result);
    }

    [Fact]
    public void 边界token句号截断否定()
    {
        var result = _detector.IsNegated("未见明确结节。肝囊肿", "肝囊肿");
        Assert.False(result);
    }

    [Fact]
    public void 排除恶性病变_恶性被否定()
    {
        var result = _detector.IsNegated("排除恶性病变", "恶性");
        Assert.True(result);
    }

    [Fact]
    public void 除外骨折_骨折被否定()
    {
        var result = _detector.IsNegated("除外骨折", "骨折");
        Assert.True(result);
    }

    [Fact]
    public void 无否定词时_不被否定()
    {
        var result = _detector.IsNegated("双肺纹理清晰", "结节");
        Assert.False(result);
    }

    [Fact]
    public void 目标词在否定词作用域外_不被否定()
    {
        var result = _detector.IsNegated("未见明确结节影，右侧胸腔积液", "积液");
        Assert.False(result);
    }

    [Fact]
    public void 排除白名单_需进一步检查()
    {
        var result = _detector.IsNegated("未见明确占位，需进一步检查", "检查");
        Assert.False(result);
    }
}
