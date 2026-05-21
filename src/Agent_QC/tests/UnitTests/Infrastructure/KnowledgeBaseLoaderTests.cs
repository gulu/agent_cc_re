using Xunit;
using Agent_QC.Entities;
using Agent_QC.Infrastructure;

namespace Agent_QC.Tests.UnitTests.Infrastructure;

public class KnowledgeBaseLoaderTests
{
    private readonly string _yaml = @"
gender_exclude_female:
- category: gender_exclude_female
  key: 子宫
  values:
  - 男
  - 未知
  severity: critical
  sort_order: 1
- category: gender_exclude_female
  key: 卵巢
  values:
  - 男
  severity: critical
  sort_order: 1
gender_exclude_male:
- category: gender_exclude_male
  key: 前列腺
  values:
  - 女
  severity: critical
  sort_order: 1
";

    [Fact]
    public void Load_ValidYaml_ReturnsAllEntries()
    {
        var loader = new KnowledgeBaseLoader();
        var result = loader.ParseYaml(_yaml);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Load_ParsesCategoryCode()
    {
        var loader = new KnowledgeBaseLoader();
        var result = loader.ParseYaml(_yaml);

        Assert.Contains(result, r => r.CategoryCode == "gender_exclude_female" && r.MatchKey == "子宫");
        Assert.Contains(result, r => r.CategoryCode == "gender_exclude_male" && r.MatchKey == "前列腺");
    }

    [Fact]
    public void Load_ParsesValues()
    {
        var loader = new KnowledgeBaseLoader();
        var result = loader.ParseYaml(_yaml);

        var uterus = result.First(r => r.MatchKey == "子宫");
        Assert.Contains("男", uterus.MatchValue);

        var ovary = result.First(r => r.MatchKey == "卵巢");
        Assert.Contains("男", ovary.MatchValue);
    }

    [Fact]
    public void Load_ParsesSeverity()
    {
        var loader = new KnowledgeBaseLoader();
        var result = loader.ParseYaml(_yaml);

        Assert.All(result, r => Assert.Equal("critical", r.Severity));
    }

    [Fact]
    public void FileExists_KnowledgeBaseYaml()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../knowledge/knowledge-base.yaml");

        Assert.True(File.Exists(path), $"Expected YAML file at: {path}");
    }
}
