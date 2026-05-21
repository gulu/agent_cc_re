// FreeSql 实体基类
// 所有数据库实体继承此类，确保公共字段一致性

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

/// <summary>
/// 实体基类 — 所有表实体继承此类
/// </summary>
public class JSBaseDBEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(IsIgnore = true)]
    public virtual string TableName => "未定义";
}
