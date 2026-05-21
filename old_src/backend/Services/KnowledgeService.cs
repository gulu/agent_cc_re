// KnowledgeService — 知识库管理服务
// 知识库 / 术语标准 / RADS 标准 的 CRUD 操作

using FreeSql;
using ReportQC.Entities;
using ReportQC.Models;

namespace ReportQC.Services;

public static class KnowledgeService
{
    // ══════════════════════════════════════════════
    //  知识库（knowledge_base）
    // ══════════════════════════════════════════════

    public static AjaxResult GetKnowledgeList(IFreeSql fsql, string? category = null, int page = 1, int pageSize = 50)
    {
        try
        {
            var query = fsql.Select<KnowledgeBase>().Where(k => k.IsActive);
            if (!string.IsNullOrEmpty(category))
                query = query.Where(k => k.CategoryCode == category);

            var total = query.Count();
            var items = query.OrderBy(k => k.CategoryCode).OrderBy(k => k.SortOrder)
                .Skip((page - 1) * pageSize).Limit(pageSize).ToList();

            return AjaxResult.Success(new { items, total, page, pageSize });
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询知识库失败：{ex.Message}");
        }
    }

    public static AjaxResult GetKnowledgeById(IFreeSql fsql, long id)
    {
        try
        {
            var item = fsql.Select<KnowledgeBase>().Where(k => k.Id == id).First();
            return item != null
                ? AjaxResult.Success(item)
                : AjaxResult.Error("记录不存在");
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }

    public static AjaxResult CreateKnowledge(IFreeSql fsql, KnowledgeBaseRequest req)
    {
        try
        {
            var entity = new KnowledgeBase
            {
                CategoryCode = req.CategoryCode,
                MatchKey = req.MatchKey,
                MatchValue = req.MatchValue,
                Description = req.Description,
                Severity = req.Severity ?? "warning",
                IsActive = true,
                SortOrder = req.SortOrder ?? 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            fsql.Insert(entity).ExecuteAffrows();
            return AjaxResult.Success(entity);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"创建失败：{ex.Message}");
        }
    }

    public static AjaxResult UpdateKnowledge(IFreeSql fsql, long id, KnowledgeBaseRequest req)
    {
        try
        {
            var existing = fsql.Select<KnowledgeBase>().Where(k => k.Id == id).First();
            if (existing == null) return AjaxResult.Error("记录不存在");

            existing.CategoryCode = req.CategoryCode;
            existing.MatchKey = req.MatchKey;
            existing.MatchValue = req.MatchValue;
            existing.Description = req.Description;
            existing.Severity = req.Severity ?? "warning";
            existing.SortOrder = req.SortOrder ?? 0;
            existing.UpdatedAt = DateTime.Now;

            fsql.Update<KnowledgeBase>().SetSource(existing).ExecuteAffrows();
            return AjaxResult.Success(existing);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"更新失败：{ex.Message}");
        }
    }

    public static AjaxResult DeleteKnowledge(IFreeSql fsql, long id)
    {
        try
        {
            // 软删除
            fsql.Update<KnowledgeBase>()
                .Set(k => k.IsActive, false)
                .Set(k => k.UpdatedAt, DateTime.Now)
                .Where(k => k.Id == id)
                .ExecuteAffrows();
            return AjaxResult.Success(null, "删除成功");
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"删除失败：{ex.Message}");
        }
    }

    public static AjaxResult ReloadCache(IFreeSql fsql)
    {
        try
        {
            QcKnowledgeCache.Reload(fsql);
            return AjaxResult.Success(null, "知识库缓存已热更新");
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"重载失败：{ex.Message}");
        }
    }

    /// <summary>获取知识库分类列表</summary>
    public static AjaxResult GetCategories(IFreeSql fsql)
    {
        try
        {
            var cats = fsql.Select<KnowledgeBase>()
                .Where(k => k.IsActive)
                .ToList(k => k.CategoryCode)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return AjaxResult.Success(cats);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询分类失败：{ex.Message}");
        }
    }

    // ══════════════════════════════════════════════
    //  术语标准（terminology_standard）
    // ══════════════════════════════════════════════

    public static AjaxResult GetTerminologyList(IFreeSql fsql, string? category = null)
    {
        try
        {
            var query = fsql.Select<TerminologyStandard>().Where(t => t.IsActive);
            if (!string.IsNullOrEmpty(category))
                query = query.Where(t => t.Category == category);

            var items = query.OrderBy(t => t.Category).ToList();
            return AjaxResult.Success(items);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }

    public static AjaxResult CreateTerminology(IFreeSql fsql, TerminologyRequest req)
    {
        try
        {
            var entity = new TerminologyStandard
            {
                StandardTerm = req.StandardTerm,
                Category = req.Category,
                NonStandardTerms = req.NonStandardTerms,
                Description = req.Description,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            fsql.Insert(entity).ExecuteAffrows();
            return AjaxResult.Success(entity);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"创建失败：{ex.Message}");
        }
    }

    public static AjaxResult DeleteTerminology(IFreeSql fsql, long id)
    {
        try
        {
            fsql.Update<TerminologyStandard>()
                .Set(t => t.IsActive, false)
                .Where(t => t.Id == id)
                .ExecuteAffrows();
            return AjaxResult.Success(null, "删除成功");
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"删除失败：{ex.Message}");
        }
    }

    // ══════════════════════════════════════════════
    //  RADS 标准（rads_standard）
    // ══════════════════════════════════════════════

    public static AjaxResult GetRadsList(IFreeSql fsql, string? radsType = null)
    {
        try
        {
            var query = fsql.Select<RadsStandard>().Where(r => r.IsActive);
            if (!string.IsNullOrEmpty(radsType))
                query = query.Where(r => r.RadsType == radsType);

            var items = query.OrderBy(r => r.RadsType).OrderBy(r => r.SortOrder).ToList();
            return AjaxResult.Success(items);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }

    public static AjaxResult GetRadsTypes(IFreeSql fsql)
    {
        try
        {
            var types = fsql.Select<RadsStandard>()
                .Where(r => r.IsActive)
                .ToList(r => r.RadsType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            return AjaxResult.Success(types);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"查询失败：{ex.Message}");
        }
    }
}
