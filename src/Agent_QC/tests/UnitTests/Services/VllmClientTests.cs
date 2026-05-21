using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class VllmClientTests
{
    private static Mock<HttpMessageHandler> CreateHandler(HttpStatusCode status, string? content = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = content != null ? new StringContent(content) : null,
            });
        return handler;
    }

    [Fact]
    public async Task 健康检查可用_返回true并标记healthy()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var client = new VllmClient(new HttpClient(handler.Object), "http://localhost:8100");

        var result = await client.CheckHealthAsync();

        Assert.True(result);
        Assert.Equal(VllmHealthStatus.Healthy, client.Health);
    }

    [Fact]
    public async Task 健康检查不可用_返回false并标记unavailable()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new VllmClient(new HttpClient(handler.Object), "http://localhost:8100");

        var result = await client.CheckHealthAsync();

        Assert.False(result);
        Assert.Equal(VllmHealthStatus.Unavailable, client.Health);
    }

    [Fact]
    public async Task 不可用时ChatAsync返回null()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new VllmClient(new HttpClient(handler.Object), "http://localhost:8100");
        await client.CheckHealthAsync(); // marks unavailable

        var result = await client.ChatAsync(new VllmChatRequest
        {
            Messages = new List<VllmMessage> { new() { Role = "user", Content = "test" } }
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task ChatAsync发送正确请求并解析响应()
    {
        var responseJson = JsonSerializer.Serialize(new VllmChatResponse
        {
            Choices = new List<VllmChoice>
            {
                new() { Message = new VllmMessage { Role = "assistant", Content = "{\"judgment\":\"fail\"}" } }
            }
        });
        var handler = CreateHandler(HttpStatusCode.OK, responseJson);
        var client = new VllmClient(new HttpClient(handler.Object), "http://localhost:8100");
        await client.CheckHealthAsync();

        var result = await client.ChatAsync(new VllmChatRequest
        {
            Model = "qwen3.5-9b",
            Messages = new List<VllmMessage> { new() { Role = "user", Content = "检查报告" } },
            MaxTokens = 100,
            Temperature = 0.1f,
        });

        Assert.NotNull(result);
        Assert.Equal("{\"judgment\":\"fail\"}", result!.FirstContent);
    }
}
