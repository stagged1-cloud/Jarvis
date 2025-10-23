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
2. Determine what action(s) the user wants to perform
3. Respond with structured JSON containing single or multiple actions

Available actions:
- open_app: Launch an application (target: app name like 'notepad', 'calc')
- type_text: Type text into active window (target: the text to type)
- wait: Pause execution (parameters: {""milliseconds"": 1000})
- press_key: Press keyboard keys (target: key name like 'enter', 'tab')
- search_web: Open browser and search (target: search query)
- control_window: Minimize, maximize, close windows (target: window title)
- move_mouse: Move mouse (parameters: {""x"": 100, ""y"": 200})
- click: Click at current position

**IMPORTANT**: For multi-step commands, return a ""steps"" array instead of single action.

Single-step format:
{
  ""action"": ""open_app"",
  ""target"": ""notepad"",
  ""confidence"": 0.95,
  ""explanation"": ""Opening Notepad""
}

Multi-step format:
{
  ""steps"": [
    {
      ""action"": ""open_app"",
      ""target"": ""notepad"",
      ""order"": 1,
      ""delayMs"": 1000,
      ""description"": ""Open Notepad""
    },
    {
      ""action"": ""type_text"",
      ""target"": ""Hello World"",
      ""order"": 2,
      ""delayMs"": 0,
      ""description"": ""Type the text""
    }
  ],
  ""confidence"": 0.95,
  ""explanation"": ""Opening Notepad and typing Hello World""
}

Examples:
- ""Open Notepad"" → single action
- ""Open Notepad and type Hello"" → multi-step with steps array
- ""Search for pizza"" → single action
- ""Open Calculator and press 5"" → multi-step

Always add delayMs between app launches (1000ms) to let apps open.
If unsure, use action: ""clarify"". If unsafe, use action: ""deny"".
Respond with ONLY valid JSON, no markdown code blocks.";

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
            // Clean up the response - remove markdown code blocks, trim whitespace
            var cleaned = responseText.Trim();
            if (cleaned.StartsWith("```json"))
            {
                cleaned = cleaned.Substring(7); // Remove ```json
            }
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3); // Remove ```
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }
            cleaned = cleaned.Trim();

            // Try to extract JSON from the response - find FIRST { and LAST }
            var jsonStart = cleaned.IndexOf('{');
            var jsonEnd = cleaned.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                // Take ONLY from first { to last }, discard any trailing garbage
                var json = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // Try to deserialize
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var parsed = System.Text.Json.JsonSerializer.Deserialize<LLMResponseJson>(json, options);

                if (parsed != null)
                {
                    var response = new LLMResponse
                    {
                        Success = true,
                        Action = parsed.action ?? "unknown",
                        Target = parsed.target,
                        Parameters = parsed.parameters ?? new Dictionary<string, object>(),
                        Message = parsed.explanation ?? responseText,
                        Confidence = parsed.confidence,
                        RawResponse = responseText
                    };

                    // Handle multi-step responses
                    if (parsed.steps != null && parsed.steps.Count > 0)
                    {
                        response.Steps = parsed.steps;
                        response.Action = "multi_step"; // Mark as multi-step workflow
                    }

                    return response;
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
        public List<ActionStep>? steps { get; set; }
    }
}
