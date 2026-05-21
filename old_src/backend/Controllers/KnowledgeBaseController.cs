// 知识库管理 API
// 路由：/api/v1/knowledge-base

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReportQC.Models;
using ReportQC.Services;

namespace ReportQC.Controllers;

[Route("api/v1/knowledge-base")]
public class KnowledgeBaseController : BaseController
{
    private readonly IFreeSql _fsql;

    public KnowledgeBaseController(IFreeSql fsql) => _fsql = fsql;

    /// <summary>获取知识库条目列表</summary>
    [HttpGet]
    public IActionResult GetList([FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = KnowledgeService.GetKnowledgeList(_fsql, category, page, pageSize);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>获取知识库分类列表</summary>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var result = KnowledgeService.GetCategories(_fsql);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>获取单条知识库条目</summary>
    [HttpGet("{id}")]
    public IActionResult GetById(long id)
    {
        var result = KnowledgeService.GetKnowledgeById(_fsql, id);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>创建知识库条目</summary>
    [HttpPost]
    public IActionResult Create([FromBody] KnowledgeBaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CategoryCode))
            return Error("CategoryCode 不能为空");
        if (string.IsNullOrWhiteSpace(req.MatchKey))
            return Error("MatchKey 不能为空");

        var result = KnowledgeService.CreateKnowledge(_fsql, req);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>更新知识库条目</summary>
    [HttpPut("{id}")]
    public IActionResult Update(long id, [FromBody] KnowledgeBaseRequest req)
    {
        var result = KnowledgeService.UpdateKnowledge(_fsql, id, req);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>删除知识库条目（软删除）</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(long id)
    {
        var result = KnowledgeService.DeleteKnowledge(_fsql, id);
        return Ok(JsonConvert.SerializeObject(result));
    }

    /// <summary>热更新知识库缓存</summary>
    [HttpPost("reload")]
    public IActionResult Reload()
    {
        var result = KnowledgeService.ReloadCache(_fsql);
        return Ok(JsonConvert.SerializeObject(result));
    }
}
