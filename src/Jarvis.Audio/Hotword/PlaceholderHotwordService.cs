using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jarvis.Audio.Hotword;

/// <summary>
/// Placeholder Hotword detection service
/// TODO: Implement with Picovoice Porcupine
/// </summary>
public class PlaceholderHotwordService : IHotwordService
{
    private readonly ILogger<PlaceholderHotwordService> _logger;
    private bool _isListening;

    public event EventHandler? HotwordDetected;

    public PlaceholderHotwordService(ILogger<PlaceholderHotwordService> logger)
    {
        _logger = logger;
    }

    public void StartListening()
    {
        if (_isListening)
        {
            _logger.LogWarning("Already listening");
            return;
        }

        _isListening = true;
        _logger.LogInformation("Started listening for hotword (placeholder)");
        
        // TODO: Implement actual hotword detection
        // For now, simulate detection after 5 seconds for testing
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            if (_isListening)
            {
                SimulateHotwordDetection();
            }
        });
    }

    public void StopListening()
    {
        if (!_isListening)
        {
            _logger.LogWarning("Not currently listening");
            return;
        }

        _isListening = false;
        _logger.LogInformation("Stopped listening for hotword (placeholder)");
    }

    private void SimulateHotwordDetection()
    {
        _logger.LogInformation("Hotword detected (simulated)!");
        HotwordDetected?.Invoke(this, EventArgs.Empty);
    }
}
