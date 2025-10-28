using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Whisper.net;

namespace Jarvis.Audio.STT;

/// <summary>
/// Speech-to-Text service using Whisper.net for local speech recognition
/// </summary>
public class WhisperSTTService : ISTTService, IDisposable
{
    private readonly ILogger<WhisperSTTService> _logger;
    private readonly string _modelPath;
    private readonly int _microphoneDeviceNumber;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private WaveInEvent? _waveIn;
    private readonly List<byte> _audioBuffer;
    private bool _isRecording;
    private bool _disposed;

    public event EventHandler<string>? SpeechRecognized;

    /// <summary>
    /// Creates a new Whisper STT service
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="modelPath">Path to the Whisper model file (.bin)</param>
    /// <param name="microphoneDeviceNumber">Microphone device number</param>
    public WhisperSTTService(
        ILogger<WhisperSTTService> logger,
        string modelPath,
        int microphoneDeviceNumber = 0)
    {
        _logger = logger;
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _microphoneDeviceNumber = microphoneDeviceNumber;
        _audioBuffer = new List<byte>();

        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"Whisper model not found at: {_modelPath}");
        }

        // Initialize Whisper factory and processor once
        _whisperFactory = WhisperFactory.FromPath(_modelPath);
        var builder = _whisperFactory.CreateBuilder()
            .WithLanguage("en");
        _whisperProcessor = builder.Build();

        _logger.LogInformation($"WhisperSTTService initialized with model: {_modelPath}");
    }

    public void StartRecording()
    {
        if (_isRecording)
        {
            _logger.LogWarning("Already recording");
            return;
        }

        try
        {
            _audioBuffer.Clear();

            // Setup audio capture at 16kHz (Whisper's expected sample rate)
            _waveIn = new WaveInEvent
            {
                DeviceNumber = _microphoneDeviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, mono
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.RecordingStopped += (s, e) =>
            {
                _logger.LogInformation("Recording stopped, processing audio...");
                ProcessAudio();
            };

            _waveIn.StartRecording();
            _isRecording = true;

            _logger.LogInformation("Started recording for speech recognition");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting recording");
            throw;
        }
    }

    public void StopRecording()
    {
        if (!_isRecording)
        {
            _logger.LogWarning("Not currently recording");
            return;
        }

        _isRecording = false;

        if (_waveIn != null)
        {
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }

        _logger.LogInformation("Stopped recording");
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Convert to short array for amplification
        short[] audioFrame = new short[e.BytesRecorded / 2];
        Buffer.BlockCopy(e.Buffer, 0, audioFrame, 0, e.BytesRecorded);

        // Apply gain boost for quiet microphones (same as hotword service)
        const float gainMultiplier = 80.0f;
        for (int i = 0; i < audioFrame.Length; i++)
        {
            float amplified = audioFrame[i] * gainMultiplier;
            if (amplified > short.MaxValue)
                audioFrame[i] = short.MaxValue;
            else if (amplified < short.MinValue)
                audioFrame[i] = short.MinValue;
            else
                audioFrame[i] = (short)amplified;
        }

        // Convert back to bytes
        byte[] buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(audioFrame, 0, buffer, 0, e.BytesRecorded);
        _audioBuffer.AddRange(buffer);
    }

    private async void ProcessAudio()
    {
        if (_audioBuffer.Count == 0)
        {
            _logger.LogWarning("No audio data to process");
            return;
        }

        try
        {
            _logger.LogInformation($"Processing {_audioBuffer.Count} bytes of audio data");

            // Convert byte array to float array for Whisper
            short[] samples = new short[_audioBuffer.Count / 2];
            Buffer.BlockCopy(_audioBuffer.ToArray(), 0, samples, 0, _audioBuffer.Count);

            // Need at least 1 second of audio (16000 samples at 16kHz)
            if (samples.Length < 16000)
            {
                _logger.LogWarning($"Audio too short: {samples.Length} samples (need at least 16000)");
                // Pad with zeros to minimum length
                Array.Resize(ref samples, 16000);
            }

            float[] floatSamples = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                floatSamples[i] = samples[i] / 32768f; // Convert to float range [-1, 1]
            }

            _logger.LogInformation($"Processing {floatSamples.Length} audio samples");

            // Process audio with Whisper
            var result = new System.Text.StringBuilder();
            await foreach (var segment in _whisperProcessor!.ProcessAsync(floatSamples))
            {
                result.Append(segment.Text);
                _logger.LogDebug($"Segment: {segment.Text}");
            }

            var transcription = result.ToString().Trim();
            
            if (!string.IsNullOrWhiteSpace(transcription))
            {
                _logger.LogInformation($"Speech recognized: {transcription}");
                OnSpeechRecognized(transcription);
            }
            else
            {
                _logger.LogWarning("No speech detected in audio");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio with Whisper");
        }
        finally
        {
            _audioBuffer.Clear();
        }
    }

    private void OnSpeechRecognized(string text)
    {
        SpeechRecognized?.Invoke(this, text);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopRecording();
            _waveIn?.Dispose();
            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
            _disposed = true;
        }
    }
}
