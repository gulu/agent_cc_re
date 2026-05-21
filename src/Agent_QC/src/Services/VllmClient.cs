using System.Net.Http.Json;
using System.Text.Json;
using Agent_QC.Models;

namespace Agent_QC.Services;

public class VllmClient : IVllmClient
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _model;
    private int _consecutiveFailures;

    public VllmHealthStatus Health { get; private set; } = VllmHealthStatus.Unavailable;

    public VllmClient(HttpClient http, string endpoint, string model = "qwen3.5-9b")
    {
        _http = http;
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_endpoint}/health");
            if (response.IsSuccessStatusCode)
            {
                Health = VllmHealthStatus.Healthy;
                _consecutiveFailures = 0;
                return true;
            }
        }
        catch { }

        _consecutiveFailures++;
        if (_consecutiveFailures >= 3)
            Health = VllmHealthStatus.Unavailable;
        return false;
    }

    public async Task<VllmChatResponse?> ChatAsync(VllmChatRequest request, CancellationToken ct = default)
    {
        if (Health != VllmHealthStatus.Healthy)
            return null;

        try
        {
            request.Model = _model;
            var response = await _http.PostAsJsonAsync(
                $"{_endpoint}/v1/chat/completions", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= 3)
                    Health = VllmHealthStatus.Unavailable;
                return null;
            }

            _consecutiveFailures = 0;
            return await response.Content.ReadFromJsonAsync<VllmChatResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
        }
        catch
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= 3)
                Health = VllmHealthStatus.Unavailable;
            return null;
        }
    }
}
