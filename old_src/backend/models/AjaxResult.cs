// 统一 API 响应格式
// 遵循 soul.md 规范：所有 API 返回 AjaxResult 封装

namespace ReportQC.Models;

public class AjaxResult
{
    public int Code { get; set; }
    public object? Data { get; set; }
    public string Msg { get; set; } = string.Empty;

    public static AjaxResult Success(object? data = null, string msg = "success")
        => new() { Code = 200, Data = data, Msg = msg };

    public static AjaxResult Error(string msg)
        => new() { Code = 500, Msg = msg };

    public static AjaxResult Error(string msg, object? data)
        => new() { Code = 500, Data = data, Msg = msg };
}
