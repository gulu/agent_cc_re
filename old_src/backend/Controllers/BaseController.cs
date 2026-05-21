// 基础控制器 — 所有 Controller 继承此类
// 提供统一的 AjaxResult 封装

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ReportQC.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// 返回成功响应
    /// </summary>
    protected IActionResult Success(object? data = null, string msg = "success")
    {
        return Ok(JsonConvert.SerializeObject(AjaxResult.Success(data, msg)));
    }

    /// <summary>
    /// 返回错误响应
    /// </summary>
    protected IActionResult Error(string msg, object? data = null)
    {
        var result = data != null
            ? AjaxResult.Error(msg, JsonConvert.SerializeObject(data))
            : AjaxResult.Error(msg);
        return Ok(JsonConvert.SerializeObject(result));
    }
}

/// <summary>
/// 统一 API 响应格式
/// </summary>
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
