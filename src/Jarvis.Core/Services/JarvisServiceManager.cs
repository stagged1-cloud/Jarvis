using Jarvis.Core.Interfaces;
using Jarvis.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Jarvis.Core.Services;

public class JarvisServiceManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JarvisServiceManager> _logger;
    private readonly JarvisConfiguration _config;

    public JarvisServiceManager(IServiceProvider serviceProvider, ILogger<JarvisServiceManager> logger, JarvisConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Jarvis services...");

        // Initialize core services
        var hotwordService = _serviceProvider.GetRequiredService<IHotwordService>();
        var sttService = _serviceProvider.GetRequiredService<ISTTService>();
        var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
        var captureService = _serviceProvider.GetRequiredService<IScreenCaptureService>();
        var ocrService = _serviceProvider.GetRequiredService<IOCRService>();
        var llmService = _serviceProvider.GetRequiredService<ILLMService>();
        var inputService = _serviceProvider.GetRequiredService<IInputControlService>();
        var guardRailService = _serviceProvider.GetRequiredService<IGuardRailService>();

        // Wire up events
        hotwordService.HotwordDetected += OnHotwordDetected;
        sttService.SpeechRecognized += OnSpeechRecognized;

        _logger.LogInformation("Jarvis services initialized successfully.");
        return Task.CompletedTask;
    }

    private async void OnHotwordDetected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Hotword detected, starting assist mode...");

        var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
        await ttsService.SpeakAsync("Yes?");

        var sttService = _serviceProvider.GetRequiredService<ISTTService>();
        sttService.StartRecording();
    }

    private async void OnSpeechRecognized(object? sender, string transcript)
    {
        _logger.LogInformation($"Speech recognized: {transcript}");

        var sttService = _serviceProvider.GetRequiredService<ISTTService>();
        sttService.StopRecording();

        // Process the command
        await ProcessCommandAsync(transcript);
    }

    private async Task ProcessCommandAsync(string command)
    {
        try
        {
            var llmService = _serviceProvider.GetRequiredService<ILLMService>();
            var captureService = _serviceProvider.GetRequiredService<IScreenCaptureService>();
            var ocrService = _serviceProvider.GetRequiredService<IOCRService>();
            var guardRailService = _serviceProvider.GetRequiredService<IGuardRailService>();

            // Capture screen context
            ScreenContext? context = null;
            if (_config.AI.UseLocalFirst || command.Contains("screen") || command.Contains("see"))
            {
                captureService.StartCapture();
                // Wait for a frame
                var frameTask = new TaskCompletionSource<byte[]>();
                void OnFrameCaptured(object? s, byte[] data) => frameTask.SetResult(data);
                captureService.FrameCaptured += OnFrameCaptured;
                var imageData = await frameTask.Task;
                captureService.FrameCaptured -= OnFrameCaptured;
                captureService.StopCapture();

                var ocrText = await ocrService.ExtractTextAsync(imageData);
                context = new ScreenContext { ImageData = imageData, OCRText = ocrText };
            }

            // Get LLM response
            var response = await llmService.ProcessQueryAsync(command, context?.OCRText);

            // Parse and execute actions
            var actions = ParseActions(response);
            foreach (var action in actions)
            {
                if (guardRailService.IsActionAllowed(action.Type, action.Target))
                {
                    if (action.RequiresApproval)
                    {
                        // TODO: Show approval UI
                        var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
                        await ttsService.SpeakAsync($"I want to {action.Type}. Approve?");
                        // For now, assume approved
                    }

                    await ExecuteActionAsync(action);
                    guardRailService.LogAction(action.Type, true);
                }
                else
                {
                    guardRailService.LogAction(action.Type, false);
                    var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
                    await ttsService.SpeakAsync("Sorry, that action is not allowed.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command");
            var ttsService = _serviceProvider.GetRequiredService<ITTSService>();
            await ttsService.SpeakAsync("Sorry, I encountered an error.");
        }
    }

    private List<ActionRequest> ParseActions(string llmResponse)
    {
        // TODO: Implement proper action parsing from LLM response
        // For now, return empty list
        return new List<ActionRequest>();
    }

    private async Task ExecuteActionAsync(ActionRequest action)
    {
        var skillManager = _serviceProvider.GetRequiredService<ISkillManager>();
        var skill = skillManager.GetSkill(action.Type);
        if (skill != null)
        {
            await skill.ExecuteAsync(action.Parameters);
        }
        else
        {
            // Fallback to direct input control
            var inputService = _serviceProvider.GetRequiredService<IInputControlService>();
            switch (action.Type.ToLower())
            {
                case "type":
                    inputService.TypeText(action.Parameters);
                    break;
                case "click":
                    inputService.ClickMouse();
                    break;
                // Add more action types
            }
        }
    }
}

public interface ISkillManager
{
    ISkill? GetSkill(string name);
    void RegisterSkill(ISkill skill);
}