// 报告查询服务 — 通过 FreeSQL 查询 Oracle v_qc_report 视图

using FreeSql;
using QCService.Models;

namespace QCService.Services;

/// <summary>报告查询服务</summary>
public class ReportQueryService
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ReportQueryService> _logger;

    public ReportQueryService(IFreeSql freeSql, ILogger<ReportQueryService> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    /// <summary>根据影像号查询报告数据（Oracle v_qc_report 视图）</summary>
    public async Task<ReportQueryResponse?> QueryByAccessNumberAsync(string accessNumber)
    {
        if (string.IsNullOrWhiteSpace(accessNumber))
        {
            _logger.LogWarning("查询报告时影像号为空");
            return null;
        }

        try
        {
            _logger.LogInformation("查询报告: AccessNumber={AccessNumber}", accessNumber);

            var entity = await _freeSql.Select<ViewQcReport>()
                .Where(r => r.AccessNumber == accessNumber)
                .FirstAsync();

            if (entity == null)
            {
                _logger.LogWarning("未查询到报告: AccessNumber={AccessNumber}", accessNumber);
                return null;
            }

            var response = ReportQueryResponse.FromEntity(entity);

            _logger.LogInformation("报告查询成功: AccessNumber={No}, Patient={Name}, HasContent={HasC}, HasDiagnosis={HasD}",
                accessNumber,
                entity.PatientName,
                !string.IsNullOrWhiteSpace(entity.ReportContent),
                !string.IsNullOrWhiteSpace(entity.ReportDiagnosis));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询报告异常: AccessNumber={AccessNumber}", accessNumber);
            throw;
        }
    }

    /// <summary>批量查询（支持多个影像号）</summary>
    public async Task<List<ReportQueryResponse>> QueryBatchAsync(List<string> accessNumbers)
    {
        if (accessNumbers == null || accessNumbers.Count == 0)
            return new List<ReportQueryResponse>();

        try
        {
            var entities = await _freeSql.Select<ViewQcReport>()
                .Where(r => accessNumbers.Contains(r.AccessNumber))
                .ToListAsync();

            return entities.Select(ReportQueryResponse.FromEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量查询报告异常");
            throw;
        }
    }

    /// <summary>检查数据库连接是否正常</summary>
    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            // 执行轻量查询检查连接
            var count = await _freeSql.Select<ViewQcReport>()
                .CountAsync();
            _logger.LogInformation("数据库连接正常，v_qc_report 视图记录数: {Count}", count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库连接检查失败");
            return false;
        }
    }
}
