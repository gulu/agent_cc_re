namespace Agent_QC.Models;

public enum VllmHealthStatus { Healthy, Unavailable }

public class VllmChatRequest
{
    public string Model { get; set; } = "/home/gulu/.cache/modelscope/hub/models/Qwen/Qwen3-4B-AWQ";
    public List<VllmMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 512;
    public float Temperature { get; set; } = 0.1f;
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
            var idx = content.IndexOf("<｜end▁of▁thinking｜>", StringComparison.Ordinal);
            if (idx >= 0)
                content = content[(idx + 9)..].TrimStart();
            // 兼容 <think>...</think> 格式
            const string thinkOpen = "<think>";
            const string thinkClose = "</think>";
            if (content.StartsWith(thinkOpen))
            {
                var closeIdx = content.IndexOf(thinkClose);
                if (closeIdx >= 0)
                    content = content[(closeIdx + thinkClose.Length)..].TrimStart();
            }
            return content;
        }
    }
}

public class VllmChoice
{
    public VllmMessage Message { get; set; } = new();
}
