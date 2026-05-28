namespace Agent_QC.Models;

public enum VllmHealthStatus { Healthy, Unavailable }

public class VllmChatRequest
{
    public string Model { get; set; } = "/home/gulu/.cache/modelscope/hub/models/Qwen/Qwen3-4B-AWQ";
    public List<VllmMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 256;
    public float Temperature { get; set; } = 0.1f;
    public ResponseFormat? ResponseFormat { get; set; }
}

public class ResponseFormat
{
    public string Type { get; set; } = "json_object";
}

public class VllmMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public class VllmChatResponse
{
    public List<VllmChoice> Choices { get; set; } = new();
    public string? FirstContent
    {
        get
        {
            var content = Choices.FirstOrDefault()?.Message?.Content;
            if (content == null) return null;
            // 剥离 Qwen3 思考标签
            const string closeTag = "</think>";
            int idx = content.IndexOf(closeTag, StringComparison.Ordinal);
            if (idx >= 0)
                content = content[(idx + closeTag.Length)..].TrimStart();
            // 剥离 markdown 代码块包装 (```json ... ```)
            content = StripMarkdownCodeBlock(content);
            return content;
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        text = text.Trim();
        const string prefix = "```json";
        const string suffix = "```";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = text[prefix.Length..];
            int end = text.LastIndexOf(suffix, StringComparison.Ordinal);
            if (end >= 0)
                text = text[..end];
        }
        else if (text.StartsWith("```", StringComparison.Ordinal))
        {
            // Generic code block without language tag
            text = text[3..];
            int end = text.LastIndexOf(suffix, StringComparison.Ordinal);
            if (end >= 0)
                text = text[..end];
        }
        return text.Trim();
    }
}

public class VllmChoice
{
    public VllmMessage Message { get; set; } = new();
}
