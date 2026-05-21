namespace Agent_QC.Models;

public enum VllmHealthStatus { Healthy, Unavailable }

public class VllmChatRequest
{
    public string Model { get; set; } = "qwen3.5-9b";
    public List<VllmMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 256;
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
    public string? FirstContent => Choices.FirstOrDefault()?.Message?.Content;
}

public class VllmChoice
{
    public VllmMessage Message { get; set; } = new();
}
