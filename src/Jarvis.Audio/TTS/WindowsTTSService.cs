using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Speech.Synthesis;
using System.Runtime.Versioning;

namespace Jarvis.Audio.TTS;

/// <summary>
/// Text-to-Speech service using Windows SAPI 5
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsTTSService : ITTSService, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;
    private readonly ILogger<WindowsTTSService> _logger;
    private bool _disposed;

    public WindowsTTSService(ILogger<WindowsTTSService> logger)
    {
        _logger = logger;
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SetOutputToDefaultAudioDevice();
        
        // Configure default voice
        var voices = _synthesizer.GetInstalledVoices();
        _logger.LogInformation($"Available voices: {voices.Count}");
        
        foreach (var voice in voices)
        {
            _logger.LogDebug($"Voice: {voice.VoiceInfo.Name}");
        }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to speak empty text");
            return;
        }

        try
        {
            _logger.LogInformation($"Speaking: {text}");
            _synthesizer.Speak(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error speaking text");
        }
    }

    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to speak empty text");
            return;
        }

        try
        {
            _logger.LogInformation($"Speaking async: {text}");
            await Task.Run(() => _synthesizer.Speak(text));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error speaking text asynchronously");
        }
    }

    public void SetVoice(string voiceName)
    {
        try
        {
            _synthesizer.SelectVoice(voiceName);
            _logger.LogInformation($"Voice set to: {voiceName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting voice to {voiceName}");
        }
    }

    public void SetRate(int rate)
    {
        // Rate range: -10 to 10
        _synthesizer.Rate = Math.Clamp(rate, -10, 10);
        _logger.LogDebug($"Speech rate set to: {rate}");
    }

    public void SetVolume(int volume)
    {
        // Volume range: 0 to 100
        _synthesizer.Volume = Math.Clamp(volume, 0, 100);
        _logger.LogDebug($"Speech volume set to: {volume}");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _synthesizer?.Dispose();
            _disposed = true;
        }
    }
}
