using System;

namespace Jarvis.Core.Models;

public class JarvisConfiguration
{
    public AudioConfig Audio { get; set; } = new();
    public CaptureConfig Capture { get; set; } = new();
    public AIConfig AI { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
}

public class AudioConfig
{
    public string HotwordModelPath { get; set; } = "models/hotword.ppn";
    public string PicovoiceAccessKey { get; set; } = "";
    public string STTModelPath { get; set; } = "models/stt";
    public string TTSVoice { get; set; } = "Microsoft Zira Desktop";
    public float STTSensitivity { get; set; } = 0.5f;
    public float MicrophoneGain { get; set; } = 10.0f; // Audio amplification for quiet microphones
}

public class CaptureConfig
{
    public int FrameRate { get; set; } = 2;
    public int Width { get; set; } = 960;
    public int Height { get; set; } = 540;
    public string Format { get; set; } = "WebP";
}

public class AIConfig
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public string LocalModelPath { get; set; } = "models/llm";
    public string ApiEndpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool UseLocalFirst { get; set; } = true;
    public int MaxTokens { get; set; } = 1000;
}

public class SecurityConfig
{
    public List<string> AllowedApps { get; set; } = new();
    public List<string> AllowedDomains { get; set; } = new();
    public bool RequireApproval { get; set; } = true;
    public string KillSwitchKey { get; set; } = "F12";
}

public class ActionRequest
{
    public string Type { get; set; } = "";
    public string Parameters { get; set; } = "";
    public string Target { get; set; } = "";
    public bool RequiresApproval { get; set; } = true;
}

public class ScreenContext
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string OCRText { get; set; } = "";
    public List<UIElement> Elements { get; set; } = new();
}

public class UIElement
{
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public Rectangle Bounds { get; set; } = new();
}

public class Rectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class LLMResponse
{
    public bool Success { get; set; }
    public string Action { get; set; } = "";
    public string? Target { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Message { get; set; } = "";
    public double Confidence { get; set; }
    public string RawResponse { get; set; } = "";
    public List<ActionStep>? Steps { get; set; }
    public bool IsMultiStep => Steps != null && Steps.Count > 0;
}

public class ActionStep
{
    public string Action { get; set; } = "";
    public string? Target { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int Order { get; set; }
    public int DelayMs { get; set; }
    public string Description { get; set; } = "";
}

public class StepResult
{
    public int StepNumber { get; set; }
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
}
