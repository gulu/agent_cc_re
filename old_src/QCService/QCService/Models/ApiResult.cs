// 统一 API 响应模型

using System.Text.Json.Serialization;

namespace QCService.Models;

/// <summary>统一 API 响应包装（PascalCase 输出，匹配 QCClient Newtonsoft.Json）</summary>
public class ApiResult
{
    [JsonPropertyName("Code")]
    public int Code { get; set; }

    [JsonPropertyName("Msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("Data")]
    public object? Data { get; set; }

    public static ApiResult Success(object? data = null, string msg = "success")
        => new() { Code = 200, Msg = msg, Data = data };

    public static ApiResult Error(string msg)
        => new() { Code = 500, Msg = msg };

    public static ApiResult Error(int code, string msg)
        => new() { Code = code, Msg = msg };
}
