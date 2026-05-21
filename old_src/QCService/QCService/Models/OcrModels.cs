// OCR 识别相关模型

using System.Text.Json.Serialization;

namespace QCService.Models;

/// <summary>OCR 识别请求</summary>
public class OcrRecognizeRequest
{
    /// <summary>区域 ID（客户端传入）</summary>
   
    public string AreaId { get; set; } = string.Empty;

    /// <summary>图片文件（通过 multipart/form-data 上传）</summary>
    public IFormFile? Image { get; set; }
}

/// <summary>OCR 识别响应（PascalCase，匹配 QCClient）</summary>
public class OcrRecognizeResponse
{
    [JsonPropertyName("AreaId")]
    public string AreaId { get; set; } = string.Empty;

    [JsonPropertyName("Text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("Confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("ProcessTimeMs")]
    public int ProcessTimeMs { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>批量 OCR 识别响应（PascalCase，匹配 QCClient）</summary>
public class BatchOcrResponse
{
    [JsonPropertyName("Results")]
    public List<OcrRecognizeResponse> Results { get; set; } = new();

    [JsonPropertyName("TotalProcessTimeMs")]
    public int TotalProcessTimeMs { get; set; }
}
