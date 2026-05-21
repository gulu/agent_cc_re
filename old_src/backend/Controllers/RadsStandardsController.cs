// RADS 分级标准管理 API
// 路由：/api/v1/rads-standards

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/rads-standards")]
public class RadsStandardsController : BaseController
{
    private readonly IFreeSql _fsql;

    public RadsStandardsController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>获取 RADS 标准列表</summary>
    [HttpGet]
    public IActionResult GetList([FromQuery] string? radsType)
    {
        var result = KnowledgeService.GetRadsList(_fsql, radsType);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>获取 RADS 类型列表</summary>
    [HttpGet("types")]
    public IActionResult GetTypes()
    {
        var result = KnowledgeService.GetRadsTypes(_fsql);
        return Ok(JsonConvert.SerializeObject(result));
    }
}
