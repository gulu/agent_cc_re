using System.Text.Encodings.Web;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Agent_QC.Entities;

namespace Agent_QC.Infrastructure;

public class KnowledgeBaseLoader
{
    private readonly IDeserializer _deserializer;

    public KnowledgeBaseLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public List<KnowledgeBase> ParseYaml(string yamlContent)
    {
        var result = new List<KnowledgeBase>();

        var raw = _deserializer.Deserialize<Dictionary<string, List<KnowledgeBaseEntry>>>(yamlContent);
        if (raw == null) return result;

        foreach (var (category, entries) in raw)
        {
            if (entries == null) continue;
            foreach (var entry in entries)
            {
                result.Add(new KnowledgeBase
                {
                    CategoryCode = entry.Category ?? category,
                    MatchKey = entry.Key ?? string.Empty,
                    MatchValue = JsonSerializer.Serialize(entry.Values ?? new List<string>(),
                        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                    Description = entry.Description,
                    Severity = entry.Severity ?? "warning",
                    SortOrder = entry.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        return result;
    }
}

/// <summary>Deserialization model matching the YAML entry structure.</summary>
public class KnowledgeBaseEntry
{
    public string? Category { get; set; }
    public string? Key { get; set; }
    public List<string>? Values { get; set; }
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public int SortOrder { get; set; }
}
