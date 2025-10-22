using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jarvis.Audio.STT;

/// <summary>
/// Placeholder Speech-to-Text service
/// TODO: Implement with Faster-Whisper or Vosk
/// </summary>
public class PlaceholderSTTService : ISTTService
{
    private readonly ILogger<PlaceholderSTTService> _logger;
    private bool _isRecording;

    public event EventHandler<string>? SpeechRecognized;

    public PlaceholderSTTService(ILogger<PlaceholderSTTService> logger)
    {
        _logger = logger;
    }

    public void StartRecording()
    {
        if (_isRecording)
        {
            _logger.LogWarning("Already recording");
            return;
        }

        _isRecording = true;
        _logger.LogInformation("Started recording (placeholder)");
        
        // TODO: Implement actual recording
        // For now, simulate after 2 seconds
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            if (_isRecording)
            {
                SimulateRecognition("open notepad");
            }
        });
    }

    public void StopRecording()
    {
        if (!_isRecording)
        {
            _logger.LogWarning("Not currently recording");
            return;
        }

        _isRecording = false;
        _logger.LogInformation("Stopped recording (placeholder)");
        
        // TODO: Process final audio buffer
    }

    private void SimulateRecognition(string text)
    {
        _logger.LogInformation($"Simulated speech recognition: {text}");
        SpeechRecognized?.Invoke(this, text);
    }
}
