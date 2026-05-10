using System.Threading.Tasks;

namespace Pigeon_Uno.Services;

public interface ISpeechService
{
    Task InitializeAsync();
    Task StopAsync();
    Task SpeakAsync(string text);
    Task SpeakAsync(string text, bool interrupt);
}
