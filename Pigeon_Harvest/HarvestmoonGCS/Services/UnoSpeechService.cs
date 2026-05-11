using System;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.Storage.Streams;

namespace HarvestmoonGCS.Services;

public class UnoSpeechService : ISpeechService
{
    private bool _initialized;
    
    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        _initialized = false;
        return Task.CompletedTask;
    }
    
    public Task SpeakAsync(string text)
    {
        return SpeakAsync(text, false);
    }
    
    public async Task SpeakAsync(string text, bool interrupt)
    {
        try
        {
            using var synthesizer = new SpeechSynthesizer();
            var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            
            // To play audio, we need a MediaPlayer or MediaElement.
            // Using MediaPlayer is cleaner for services.
            var player = BackgroundMediaPlayer.Current;
            // BackgroundMediaPlayer might be tricky in Uno.
            // Better to use a new MediaPlayer instance if possible, or MediaElement on page.
            // But Service is singleton.
            
            // Uno supports MediaPlayer.
            var mediaPlayer = new MediaPlayer();
            mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStream(stream, stream.ContentType);
            mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Speech failed: {ex.Message}");
        }
    }
}
