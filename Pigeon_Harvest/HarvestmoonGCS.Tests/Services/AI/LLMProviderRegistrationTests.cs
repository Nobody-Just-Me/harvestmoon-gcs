using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Tests.Services.AI;

public class LLMProviderRegistrationTests
{
    [Theory]
    [InlineData("OpenRouter", "OpenRouter")]
    [InlineData("Gemini", "Gemini")]
    [InlineData("OpenAI", "OpenAI")]
    [InlineData("Grok", "Grok")]
    [InlineData("xAI", "Grok")]
    public void AddAIServices_UsesConfiguredPrimaryProvider(string provider, string expectedProvider)
    {
        var services = new ServiceCollection();

        services.AddAIServices(settings =>
        {
            settings.Provider = provider;
            settings.FallbackProvider = "OpenRouter";
            settings.ApiKey = "test-key";
        });

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<LLMServiceFactory>();

        factory.PrimaryService.ProviderName.Should().Be(expectedProvider);
        sp.GetRequiredService<ILLMService>().ProviderName.Should().Be(expectedProvider);
    }

    [Fact]
    public void AddAIServices_UsesConfiguredFallbackProvider()
    {
        var services = new ServiceCollection();

        services.AddAIServices(settings =>
        {
            settings.Provider = "OpenAI";
            settings.FallbackProvider = "Gemini";
            settings.ApiKey = "test-key";
        });

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<LLMServiceFactory>();

        factory.PrimaryService.ProviderName.Should().Be("OpenAI");
        factory.FallbackService.ProviderName.Should().Be("Gemini");
    }

    [Fact]
    public void AddAIServices_MigratesOpenRouterFallbackToGemini()
    {
        var services = new ServiceCollection();

        services.AddAIServices(settings =>
        {
            settings.Provider = "OpenRouter";
            settings.FallbackProvider = "OpenRouter";
            settings.ApiKey = "test-key";
        });

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<LLMServiceFactory>();

        factory.PrimaryService.ProviderName.Should().Be("OpenRouter");
        factory.FallbackService.ProviderName.Should().Be("Gemini");
    }
}
