using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

public class EntityBase
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }
}
