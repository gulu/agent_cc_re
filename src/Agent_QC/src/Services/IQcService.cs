using Agent_QC.Models;

namespace Agent_QC.Services;

public interface IQcService
{
    Task<AjaxResult> ExecuteQcAsync(QcRequest request);
}
