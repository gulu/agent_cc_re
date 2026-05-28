using Agent_QC.Entities;
using Agent_QC.Models;
using FreeSql;

namespace Agent_QC.Services;

public class ReportQueryService
{
    private readonly IFreeSql _freeSql;

    public ReportQueryService(IFreeSql freeSql)
    {
        _freeSql = freeSql;
    }

    public async Task<ReportQueryResponse?> QueryByAccessNumberAsync(string accessNumber)
    {
        if (string.IsNullOrWhiteSpace(accessNumber))
            return null;

        try
        {
            var entity = await _freeSql.Select<ViewQcReport>()
                .Where(r => r.AccessNumber == accessNumber)
                .FirstAsync();

            if (entity == null) return null;

            return new ReportQueryResponse
            {
                AccessNumber = entity.AccessNumber,
                PatientName = entity.PatientName,
                PatientGender = entity.PatientSex,
                PatientAgeStr = entity.PatientAge,
                ExamType = entity.ExamType,
                ExamBodyPart = entity.ExamBodyPart,
                ClinicalHistory = entity.ClinicalHistory,
                Department = entity.Department,
                ReportContent = entity.ReportContent,
                ReportDiagnosis = entity.ReportDiagnosis,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ReportQuery] Error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            var count = await _freeSql.Select<ViewQcReport>().CountAsync();
            Console.WriteLine($"[Oracle] VIEW_QC_REPORT 连接正常，记录数: {count}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Oracle] 连接失败: {ex.Message}");
            return false;
        }
    }
}
