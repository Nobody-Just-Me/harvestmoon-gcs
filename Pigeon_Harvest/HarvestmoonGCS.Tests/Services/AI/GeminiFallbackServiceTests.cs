using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using HarvestmoonGCS.Core.Services.AI;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Tests.Services.AI;

public class GeminiFallbackServiceTests
{
    private const string TestApiKey = "test-gemini-api-key-123";
    private const string TestPrompt = "Analyze this telemetry data";

    private static GeminiFallbackService CreateService(
        HttpMessageHandler handler,
        string apiKey = TestApiKey,
        string model = "gemini-2.0-flash")
    {
        var httpClient = new HttpClient(handler);
        return new GeminiFallbackService(httpClient, apiKey, model);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public FakeHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new HttpResponseMessage
            {
                StatusCode = _statusCode,
                Content = new StringContent(_responseContent)
            };
        }
    }

    [Fact]
    public void Constructor_WithValidParams_SetsProperties()
    {
        var apiKey = "my-api-key";
        var model = "gemini-2.0-flash-lite";
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler, apiKey: apiKey, model: model);

        service.ProviderName.Should().Be("Gemini");
        service.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_SetsIsAvailableFalse()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler, apiKey: string.Empty);

        service.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithApiKeyResolver_UpdatesAvailabilityDynamically()
    {
        var dynamicKey = string.Empty;
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var service = new GeminiFallbackService(httpClient, () => dynamicKey, "gemini-2.0-flash");

        service.IsAvailable.Should().BeFalse();

        dynamicKey = "runtime-key";
        service.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithValidResponse_ReturnsSuccessResult()
    {
        var responseJson = @"{
            ""candidates"": [{
                ""content"": {
                    ""parts"": [{
                        ""text"": ""Flight data looks normal. No anomalies detected.""
                    }]
                }
            }]
        }";
        var handler = new FakeHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        var result = await service.GenerateAsync(TestPrompt);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Text.Should().Be("Flight data looks normal. No anomalies detected.");
        result.ModelUsed.Should().Be("gemini-2.0-flash");
        result.WasFallback.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WhenHttpRequestFails_ReturnsFailureResult()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, @"{""error"": {""message"": ""Invalid""}}");
        var service = CreateService(handler);

        var result = await service.GenerateAsync(TestPrompt);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("BadRequest");
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseHasNoCandidates_ReturnsFailureResult()
    {
        var responseJson = @"{}";
        var handler = new FakeHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        var result = await service.GenerateAsync(TestPrompt);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No response candidates");
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseHasEmptyText_ReturnsEmptySuccess()
    {
        var responseJson = @"{
            ""candidates"": [{
                ""content"": {
                    ""parts"": [{
                        ""text"": """"
                    }]
                }
            }]
        }";
        var handler = new FakeHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        var result = await service.GenerateAsync(TestPrompt);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenApiKeyEmpty_ReturnsFalse()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler, apiKey: string.Empty);

        var result = await service.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public void GetHealthStatus_ReturnsCorrectProviderInfo()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var status = service.GetHealthStatus();

        status.Should().NotBeNull();
        status.PrimaryModel.Should().Be("gemini-2.0-flash");
        status.FallbackModel.Should().BeEmpty();
    }

    [Fact]
    public void Service_ImplementsILLMService()
    {
        typeof(GeminiFallbackService).Should().Implement<ILLMService>();
    }
}
