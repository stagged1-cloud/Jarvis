using Jarvis.Core.Interfaces;
using Jarvis.Core.Models;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Jarvis.AI.LLM;

/// <summary>
/// LLM service using Ollama for local AI inference
/// </summary>
public class OllamaLLMService : ILLMService
{
    private readonly ILogger<OllamaLLMService> _logger;
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly string _systemPrompt;

    public event EventHandler<LLMResponse>? ResponseReceived;

    public OllamaLLMService(
        ILogger<OllamaLLMService> logger,
        string baseUrl = "http://localhost:11434",
        string model = "llama3.2")
    {
        _logger = logger;
        _model = model;
        _ollama = new OllamaApiClient(baseUrl);

        _systemPrompt = @"You are Jarvis, an AI assistant that helps users control their Windows computer through voice commands.

Your role:
1. Analyze user voice commands and screen context
2. Determine what action the user wants to perform
3. Respond with structured JSON containing the action to take

Available actions:
- open_app: Launch an application (e.g., notepad, browser)
- type_text: Type text into the active window
- move_mouse: Move mouse to coordinates
- click: Click at current or specified position
- press_key: Press keyboard keys
- search_web: Open browser and search
- control_window: Minimize, maximize, close windows

Response format (JSON only, no markdown):
{
  ""action"": ""action_name"",
  ""target"": ""target_application_or_text"",
  ""parameters"": { /* action-specific params */ },
  ""confidence"": 0.95,
  ""explanation"": ""Brief explanation of what you understood""
}

If you're unsure, ask for clarification with action: ""clarify"".
If the command is unsafe or unclear, use action: ""deny"" with explanation.

Be concise and always respond with valid JSON.";

        _logger.LogInformation($"OllamaLLMService initialized with model: {_model} at {baseUrl}");
    }

    public async Task<LLMResponse> ProcessCommand(string command, string? screenContext = null)
    {
        try
        {
            _logger.LogInformation($"Processing command: {command}");

            // Build the user prompt with command and optional screen context
            var userPrompt = $"User command: \"{command}\"";
            if (!string.IsNullOrWhiteSpace(screenContext))
            {
                userPrompt += $"\n\nScreen context:\n{screenContext}";
            }

            _logger.LogDebug($"Sending to Ollama: {userPrompt}");

            // Build full prompt with system message and user input
            var fullPrompt = $"{_systemPrompt}\n\n{userPrompt}";

            // Create generate request
            var generateRequest = new GenerateRequest
            {
                Model = _model,
                Prompt = fullPrompt
            };

            // Get response from Ollama
            var responseText = "";
            
            await foreach (var stream in _ollama.GenerateAsync(generateRequest))
            {
                responseText += stream?.Response ?? "";
            }

            _logger.LogInformation($"LLM response: {responseText}");

            // Parse the response
            var llmResponse = ParseResponse(responseText, command);
            
            OnResponseReceived(llmResponse);
            return llmResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command with LLM");
            
            var errorResponse = new LLMResponse
            {
                Success = false,
                Action = "error",
                Message = $"LLM error: {ex.Message}",
                Confidence = 0.0
            };
            
            OnResponseReceived(errorResponse);
            return errorResponse;
        }
    }

    public async Task<string> GenerateResponse(string prompt)
    {
        try
        {
            var generateRequest = new GenerateRequest
            {
                Model = _model,
                Prompt = prompt
            };

            var responseText = "";
            await foreach (var stream in _ollama.GenerateAsync(generateRequest))
            {
                responseText += stream?.Response ?? "";
            }
            
            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response");
            return $"Error: {ex.Message}";
        }
    }

    private LLMResponse ParseResponse(string responseText, string originalCommand)
    {
        try
        {
            // Try to extract JSON from the response (LLM might wrap it in markdown)
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = System.Text.Json.JsonSerializer.Deserialize<LLMResponseJson>(json);

                if (parsed != null)
                {
                    return new LLMResponse
                    {
                        Success = true,
                        Action = parsed.action ?? "unknown",
                        Target = parsed.target,
                        Parameters = parsed.parameters ?? new Dictionary<string, object>(),
                        Message = parsed.explanation ?? responseText,
                        Confidence = parsed.confidence,
                        RawResponse = responseText
                    };
                }
            }

            // If parsing fails, return as a clarification request
            _logger.LogWarning($"Could not parse LLM response as JSON: {responseText}");
            return new LLMResponse
            {
                Success = false,
                Action = "clarify",
                Message = responseText,
                Confidence = 0.5,
                RawResponse = responseText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing LLM response");
            return new LLMResponse
            {
                Success = false,
                Action = "error",
                Message = $"Parse error: {ex.Message}",
                Confidence = 0.0,
                RawResponse = responseText
            };
        }
    }

    private void OnResponseReceived(LLMResponse response)
    {
        ResponseReceived?.Invoke(this, response);
    }

    // Helper class for JSON deserialization
    private class LLMResponseJson
    {
        public string? action { get; set; }
        public string? target { get; set; }
        public Dictionary<string, object>? parameters { get; set; }
        public double confidence { get; set; }
        public string? explanation { get; set; }
    }
}
