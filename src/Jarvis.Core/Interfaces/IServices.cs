using System;

namespace Jarvis.Core.Interfaces;

public interface IHotwordService
{
    event EventHandler HotwordDetected;
    void StartListening();
    void StopListening();
}

public interface ISTTService
{
    event EventHandler<string> SpeechRecognized;
    void StartRecording();
    void StopRecording();
}

public interface ITTSService
{
    void Speak(string text);
    Task SpeakAsync(string text);
}

public interface IScreenCaptureService
{
    event EventHandler<byte[]> FrameCaptured;
    void StartCapture();
    void StopCapture();
}

public interface IOCRService
{
    Task<string> ExtractTextAsync(byte[] imageData);
}

public interface ILLMService
{
    Task<string> ProcessQueryAsync(string query, string? context = null);
}

public interface ISkill
{
    string Name { get; }
    string Description { get; }
    Task ExecuteAsync(string parameters);
}

public interface IInputControlService
{
    void MoveMouse(int x, int y);
    void ClickMouse();
    void TypeText(string text);
    void PressKey(string key);
}

public interface IGuardRailService
{
    bool IsActionAllowed(string action, string target);
    void LogAction(string action, bool approved);
}