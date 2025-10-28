# Jarvis AI Assistant - AI Agent Instructions

## Project Overview

Jarvis is a **voice-controlled Windows desktop AI assistant** inspired by Iron Man's Jarvis. It monitors screens in real-time, processes voice commands, and executes actions via mouse/keyboard control with explicit user permission. Built with .NET 9, this is a **security-first, offline-capable** system that uses local AI models when possible and escalates to cloud APIs only for complex tasks.

**Current Status**: Voice control pipeline is **fully functional** (hotword detection, STT, TTS working). Next phases: LLM integration and screen capture.

## Architecture & Data Flow

```
User says "Hey Jarvis" 
  → Porcupine Hotword Detection (local, ~1% CPU)
  → TTS responds "Yes?" 
  → Whisper STT captures command (local)
  → [PENDING] Screen capture + OCR for context
  → [PENDING] LLM processes command + screen context
  → GuardRail security check
  → [PENDING] Permission approval UI
  → Execute action via InputControlService
  → TTS speaks result
```

## Project Structure

- **Jarvis.Core**: Service interfaces (`IServices.cs`), models (`Models.cs`), orchestration (`JarvisServiceManager.cs`)
- **Jarvis.Audio**: Hotword (Porcupine), STT (Whisper.net), TTS (Windows SAPI)
- **Jarvis.AI**: [Empty] LLM integration (planned: Ollama local-first)
- **Jarvis.Capture**: [WinUI required] Windows.Graphics.Capture for screen monitoring
- **Jarvis.Input**: Win32 SendInput + UI Automation for mouse/keyboard control
- **Jarvis.UI**: [WinUI required] WinUI 3 app with permission dialogs
- **Jarvis.TestConsole**: Fully functional voice interaction test harness

**Build Note**: Core services build via `dotnet build`. WinUI projects (UI, Capture) require Visual Studio 2022 with Windows App SDK.

## Critical Patterns & Conventions

### 1. Service-Oriented Architecture with DI
All services implement interfaces in `Jarvis.Core/Interfaces/IServices.cs`. Expect dependency injection via `IServiceProvider`:

```csharp
// Services resolve dependencies via constructor injection
public class JarvisServiceManager
{
    private readonly IServiceProvider _serviceProvider;
    public JarvisServiceManager(IServiceProvider serviceProvider, ...)
    {
        var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
    }
}
```

### 2. Event-Driven Communication
Services communicate via events, **not direct calls**:

```csharp
// Hotword detected → chain to STT
hotwordService.HotwordDetected += async (sender, e) => {
    await ttsService.SpeakAsync("Yes?");
    sttService.StartRecording();
};

sttService.SpeechRecognized += async (sender, transcript) => {
    await ProcessCommandAsync(transcript);
};
```

### 3. Configuration System
All settings live in `config/appsettings.json` wrapped by `JarvisConfiguration` model. Path resolution searches upward from binary location:

```csharp
// Find config starting from AppDomain.CurrentDomain.BaseDirectory
var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
while (currentDir != null) {
    var testPath = Path.Combine(currentDir.FullName, "config", "appsettings.json");
    if (File.Exists(testPath)) { configPath = testPath; break; }
    currentDir = currentDir.Parent;
}
```

**Important config keys**:
- `Audio.PicovoiceAccessKey`: Required for hotword (get from console.picovoice.ai)
- `Audio.HotwordModelPath`: Relative to project root (e.g., `models/Hey-Jarvis_en_windows_v3_0_0.ppn`)
- `Audio.STTModelPath`: Whisper model path (download via `scripts/download-whisper-model.ps1`)
- `Security.AllowedApps`: Whitelist for app control (e.g., `["notepad.exe", "chrome.exe"]`)

### 4. Audio Pipeline Specifics
**Microphone device selection**: User selects at runtime via console prompt in TestConsole. Services accept `deviceNumber` parameter (0 = default).

**Audio amplification**: Intel Smart Sound and laptop mics are often very quiet. Apply aggressive gain in `PorcupineHotwordService`:

```csharp
const float gainMultiplier = 80.0f; // High boost for quiet mics
for (int i = 0; i < audioFrame.Length; i++) {
    float amplified = audioFrame[i] * gainMultiplier;
    audioFrame[i] = (short)Math.Clamp(amplified, short.MinValue, short.MaxValue);
}
```

**Logging audio levels**: Log periodically to debug mic issues (`🔊` = speech detected, `🔇` = silence).

### 5. Security & GuardRail Pattern
**All actions must pass GuardRail checks** before execution:

```csharp
if (guardRailService.IsActionAllowed(action.Type, action.Target)) {
    await ExecuteActionAsync(action);
    guardRailService.LogAction(action.Type, approved: true);
} else {
    guardRailService.LogAction(action.Type, approved: false);
    await ttsService.SpeakAsync("Sorry, that action is not allowed.");
}
```

Whitelist enforcement is in `config/appsettings.json` → `Security.AllowedApps`.

### 6. LLM Response Structure
When implementing LLM integration (`Jarvis.AI/LLM`), expect structured responses matching `LLMResponse` model:

```csharp
public class LLMResponse {
    public bool Success { get; set; }
    public string Action { get; set; }  // "open", "type", "click", "clarify", "deny"
    public string? Target { get; set; } // e.g., "notepad.exe"
    public Dictionary<string, object> Parameters { get; set; }
    public string Message { get; set; } // User-facing explanation
    public double Confidence { get; set; }
    public List<ActionStep>? Steps { get; set; } // For multi-step workflows
}
```

**Tiered routing strategy**: Use local Ollama for simple tasks (e.g., "open notepad"), escalate to cloud API only for complex reasoning (e.g., "analyze spreadsheet and propose cashflow plan").

### 7. Input Control Philosophy
Prefer **UI Automation over pixel clicking** when possible. SendInput is for fallback:

```csharp
// Prefer UIA (deterministic, safer):
var element = AutomationElement.RootElement.FindFirst(...);
var invokePattern = (InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern);
invokePattern.Invoke();

// Fallback to SendInput for typing:
inputService.TypeText("Hello world");
```

### 8. Testing Workflow
**Run TestConsole** to validate end-to-end voice pipeline:

```powershell
dotnet run --project src/Jarvis.TestConsole/Jarvis.TestConsole.csproj
```

Test flow: Select mic → Say "Hey Jarvis" → Hear "Yes?" → Say command → See transcription → Hear AI response.

### 9. Error Handling & Async Patterns
- **Avoid `async void`** except for event handlers
- **Always log exceptions** via `ILogger<T>`
- **Use `TaskCompletionSource`** for bridging events to async/await:

```csharp
var frameTask = new TaskCompletionSource<byte[]>();
void OnFrameCaptured(object? s, byte[] data) => frameTask.SetResult(data);
captureService.FrameCaptured += OnFrameCaptured;
var imageData = await frameTask.Task;
captureService.FrameCaptured -= OnFrameCaptured;
```

### 10. Resource Management
**Always dispose services** implementing `IDisposable`:

```csharp
public class PorcupineHotwordService : IHotwordService, IDisposable {
    public void Dispose() {
        StopListening();
        _waveIn?.Dispose();
        _porcupine?.Dispose();
    }
}
```

Use `using` statements in TestConsole and ensure `Dispose()` is called on shutdown.

## Development Commands

```powershell
# Build all projects (except WinUI)
dotnet build Jarvis.sln

# Run voice interaction test
dotnet run --project src/Jarvis.TestConsole/Jarvis.TestConsole.csproj

# Download Whisper models (141MB for base model)
.\scripts\download-whisper-model.ps1 -ModelSize base

# Run tests
dotnet test
```

## Next Steps (Priority Order)

1. **LLM Integration** (Phase 3 - IN PROGRESS):
   - Implement `OllamaLLMService` in `Jarvis.AI/LLM/`
   - Use `http://localhost:11434` (config: `AI.OllamaBaseUrl`)
   - Parse commands into `LLMResponse` with action/target/parameters
   - Test with Ollama models: llama3.2, phi-4, qwen2.5-vl

2. **Screen Capture** (Phase 3):
   - Implement `Windows.Graphics.Capture` in `Jarvis.Capture/`
   - Capture at 2 FPS, 960x540, compress to WebP
   - Add OCR via `Windows.Media.Ocr` or Tesseract
   - Feed OCR text to LLM as screen context

3. **Action Execution** (Phase 3):
   - Complete `ActionExecutionService` to execute `LLMResponse` actions
   - Implement skills in `Jarvis.Core/Skills/` (e.g., Notepad, browser control)
   - Register skills via `SkillManager`

4. **UI Polish** (Phase 4):
   - Build WinUI 3 app in Visual Studio 2022
   - Implement permission approval dialog
   - Add system tray icon, kill switch (F12)

## Common Pitfalls & Gotchas

- **WinUI build errors**: Must use Visual Studio 2022 with Windows App SDK workload. Command-line builds fail.
- **Audio silence issues**: Laptop mics often need 50-100x gain. See `gainMultiplier` in `PorcupineHotwordService.cs`.
- **Model paths**: Always resolve relative to project root found via config search, not `Environment.CurrentDirectory`.
- **Porcupine sensitivity**: Default 0.5 is too low. Use 0.75+ for better detection (set in `PorcupineHotwordService` constructor).
- **Async event handlers**: Can't `await` in event handlers directly. Use `async void` pattern or fire-and-forget with proper error handling.

## External Dependencies

- **Picovoice Porcupine**: Custom wake word "Hey Jarvis" (model included: `models/Hey-Jarvis_en_windows_v3_0_0.ppn`)
- **Whisper.net**: Local STT (model download required via script)
- **NAudio**: 16kHz, 16-bit mono PCM audio capture
- **Ollama**: [PENDING] Local LLM server (install from ollama.ai)
- **Windows.Graphics.Capture**: [PENDING] Requires Win10 1903+

## Design Philosophy

1. **Offline-first**: All audio processing runs locally (no cloud unless complex task)
2. **Security-first**: Explicit approval required (banner + kill switch)
3. **Low resource**: Target <100MB RAM idle, <5% CPU during hotword listening
4. **Modular skills**: Actions implemented as plugins via `ISkill` interface
5. **Privacy-focused**: No open mic, session logging, screen redaction zones planned

## Key Files to Reference

- `src/Jarvis.Core/Interfaces/IServices.cs` - All service contracts
- `src/Jarvis.Core/Models/Models.cs` - Configuration and data models
- `src/Jarvis.TestConsole/Program.cs` - Complete working example of voice pipeline
- `config/appsettings.json` - All runtime configuration
- `docs/PROJECT_STATUS.md` - Detailed project status and test results

## Example: Adding a New Skill

```csharp
// 1. Implement ISkill interface
public class NotepadSkill : ISkill
{
    public string Name => "notepad";
    public string Description => "Open Notepad and type text";

    public async Task ExecuteAsync(string parameters)
    {
        // Parse parameters (JSON string)
        var param = JsonSerializer.Deserialize<NotepadParams>(parameters);
        
        // Launch notepad
        Process.Start("notepad.exe");
        await Task.Delay(1000); // Wait for window
        
        // Type text via InputControlService
        var inputService = ...; // Get from DI
        inputService.TypeText(param.Text);
    }
}

// 2. Register in SkillManager
skillManager.RegisterSkill(new NotepadSkill());

// 3. LLM returns: { "Action": "notepad", "Parameters": { "Text": "Hello world" } }
```

## Questions to Ask Before Implementing

- **Does this action need approval?** Check `RequireApproval` in config.
- **Is there an existing skill?** Check `SkillManager.GetAllSkills()` before creating new.
- **Is this local or cloud?** Follow tiered routing: local rules → free model → paid API.
- **What's the error handling?** Always wrap in try-catch and speak error via TTS.
- **Is the mic device configurable?** Accept `deviceNumber` parameter for audio services.

---

**Last Updated**: October 28, 2025  
**Project Status**: Phase 2 Complete (Voice Control Working), Phase 3 In Progress (LLM + Screen Capture)
