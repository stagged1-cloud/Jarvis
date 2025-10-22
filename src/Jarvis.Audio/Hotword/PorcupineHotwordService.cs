using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Pv;

namespace Jarvis.Audio.Hotword;

/// <summary>
/// Hotword detection service using Picovoice Porcupine with real microphone input
/// </summary>
public class PorcupineHotwordService : IHotwordService, IDisposable
{
    private readonly ILogger<PorcupineHotwordService> _logger;
    private readonly string _accessKey;
    private readonly string _modelPath;
    private readonly int _microphoneDeviceNumber;
    private Porcupine? _porcupine;
    private WaveInEvent? _waveIn;
    private bool _isListening;
    private bool _disposed;

    public event EventHandler? HotwordDetected;

    /// <summary>
    /// Creates a new Porcupine hotword service
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="accessKey">Picovoice access key from https://console.picovoice.ai/</param>
    /// <param name="modelPath">Path to the .ppn keyword file</param>
    /// <param name="microphoneDeviceNumber">Microphone device number (0 = default, -1 = prompt user)</param>
    public PorcupineHotwordService(
        ILogger<PorcupineHotwordService> logger,
        string accessKey,
        string modelPath,
        int microphoneDeviceNumber = 0)
    {
        _logger = logger;
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _microphoneDeviceNumber = microphoneDeviceNumber;

        if (string.IsNullOrWhiteSpace(_accessKey))
        {
            throw new ArgumentException("Access key cannot be empty", nameof(accessKey));
        }

        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"Keyword model not found at: {_modelPath}");
        }

        _logger.LogInformation($"PorcupineHotwordService initialized with model: {_modelPath}");
    }

    public void StartListening()
    {
        if (_isListening)
        {
            _logger.LogWarning("Already listening for hotword");
            return;
        }

        try
        {
            // Initialize Porcupine with higher sensitivity
            _porcupine = Porcupine.FromKeywordPaths(
                accessKey: _accessKey,
                keywordPaths: new[] { _modelPath },
                sensitivities: new[] { 0.75f } // Increased from 0.5 for better detection
            );

            _logger.LogInformation($"Porcupine initialized. Frame length: {_porcupine.FrameLength}, Sample rate: {_porcupine.SampleRate}");

            // List available audio devices
            int deviceCount = WaveInEvent.DeviceCount;
            _logger.LogInformation($"Available microphones: {deviceCount}");
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                _logger.LogInformation($"  Device {i}: {caps.ProductName} ({caps.Channels} channels)");
            }

            // Determine which device to use
            int deviceToUse = _microphoneDeviceNumber;
            if (deviceToUse < 0 || deviceToUse >= deviceCount)
            {
                _logger.LogWarning($"Invalid device number {deviceToUse}, using default (0)");
                deviceToUse = 0;
            }

            var selectedCaps = WaveInEvent.GetCapabilities(deviceToUse);
            _logger.LogInformation($"Using Device {deviceToUse}: {selectedCaps.ProductName}");

            // Setup audio capture
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceToUse,
                WaveFormat = new WaveFormat(_porcupine.SampleRate, 16, 1), // 16-bit mono PCM
                BufferMilliseconds = (int)(_porcupine.FrameLength * 1000.0 / _porcupine.SampleRate)
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.RecordingStopped += (s, e) =>
            {
                _logger.LogInformation("Recording stopped");
            };

            _waveIn.StartRecording();
            _isListening = true;

            _logger.LogInformation("Started listening for hotword with microphone");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting hotword detection");
            throw;
        }
    }

    public void StopListening()
    {
        if (!_isListening)
        {
            _logger.LogWarning("Not currently listening");
            return;
        }

        _isListening = false;

        if (_waveIn != null)
        {
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }

        _porcupine?.Dispose();
        _porcupine = null;

        _logger.LogInformation("Stopped listening for hotword");
    }

    private int _audioCallbackCount = 0;

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_porcupine == null || !_isListening)
            return;

        try
        {
            _audioCallbackCount++;
            
            // Log every 50th callback to show audio is flowing
            if (_audioCallbackCount % 50 == 0)
            {
                _logger.LogInformation($"Audio callback #{_audioCallbackCount}, bytes: {e.BytesRecorded}");
            }

            // Convert byte array to short array (16-bit PCM)
            short[] audioFrame = new short[e.BytesRecorded / 2];
            Buffer.BlockCopy(e.Buffer, 0, audioFrame, 0, e.BytesRecorded);

            // Calculate audio level for debugging
            double audioLevel = 0;
            int maxAmplitude = 0;
            for (int i = 0; i < audioFrame.Length; i++)
            {
                int absValue = Math.Abs(audioFrame[i]);
                audioLevel += absValue;
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }
            audioLevel /= audioFrame.Length;

            // Log audio levels periodically (lowered threshold to 100)
            if (_audioCallbackCount % 20 == 0)
            {
                _logger.LogInformation($"Audio level: avg={audioLevel:F0}, max={maxAmplitude} {(audioLevel > 100 ? "🔊" : "🔇")}");
            }

            // Process audio in chunks of frame length
            int frameLength = _porcupine.FrameLength;
            for (int i = 0; i + frameLength <= audioFrame.Length; i += frameLength)
            {
                short[] frame = new short[frameLength];
                Array.Copy(audioFrame, i, frame, 0, frameLength);

                int keywordIndex = _porcupine.Process(frame);
                if (keywordIndex >= 0)
                {
                    _logger.LogInformation($"🎤 Hotword detected! (keyword index: {keywordIndex})");
                    OnHotwordDetected();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio frame");
        }
    }

    private void OnHotwordDetected()
    {
        HotwordDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopListening();
            _waveIn?.Dispose();
            _porcupine?.Dispose();
            _disposed = true;
        }
    }
}
