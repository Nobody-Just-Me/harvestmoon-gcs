namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Secure store abstraction for API keys used by AI providers.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>
    /// Reads API key for the specified provider from secure storage.
    /// Supported providers: OpenRouter, Gemini, OpenAI, Grok/XAI.
    /// </summary>
    string? GetApiKey(string provider);

    /// <summary>
    /// Saves API key for the specified provider into secure storage.
    /// </summary>
    bool SaveApiKey(string provider, string apiKey);

    /// <summary>
    /// Removes API key for the specified provider from secure storage.
    /// </summary>
    bool RemoveApiKey(string provider);

    /// <summary>
    /// Reads OpenRouter API key from secure storage.
    /// </summary>
    string? GetOpenRouterApiKey();

    /// <summary>
    /// Saves OpenRouter API key into secure storage.
    /// </summary>
    bool SaveOpenRouterApiKey(string apiKey);

    /// <summary>
    /// Removes OpenRouter API key from secure storage.
    /// </summary>
    bool RemoveOpenRouterApiKey();
}
