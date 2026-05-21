// 术语标准管理 API
// 路由：/api/v1/terminology

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Models;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/terminology")]
public class TerminologyController : BaseController
{
    private readonly IFreeSql _fsql;

    public TerminologyController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>获取术语标准列表</summary>
    [HttpGet]
    public IActionResult GetList([FromQuery] string? category)
    {
        var result = KnowledgeService.GetTerminologyList(_fsql, category);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>创建术语标准</summary>
    [HttpPost]
    public IActionResult Create([FromBody] TerminologyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StandardTerm))
            return Error("StandardTerm 不能为空");

        var result = KnowledgeService.CreateTerminology(_fsql, req);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>删除术语标准（软删除）</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(long id)
    {
        var result = KnowledgeService.DeleteTerminology(_fsql, id);
        return Ok(JsonConvert.SerializeObject(result));
    }
}
