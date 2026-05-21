using Agent_QC.Models;

namespace Agent_QC.Services;

public interface IVllmClient
{
    VllmHealthStatus Health { get; }
    Task<bool> CheckHealthAsync();
    Task<VllmChatResponse?> ChatAsync(VllmChatRequest request, CancellationToken ct = default);
}
