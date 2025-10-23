using System.Diagnostics;
using Jarvis.Core.Interfaces;
using Jarvis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Services;

/// <summary>
/// Executes actions based on LLM command understanding
/// </summary>
public class ActionExecutionService : IActionExecutionService
{
    private readonly IGuardRailService _guardRail;
    private readonly ILogger<ActionExecutionService> _logger;

    public ActionExecutionService(
        IGuardRailService guardRail,
        ILogger<ActionExecutionService> logger)
    {
        _guardRail = guardRail;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(LLMResponse command)
    {
        try
        {
            _logger.LogInformation($"Executing action: {command.Action} → {command.Target}");

            return command.Action.ToLowerInvariant() switch
            {
                "open_app" => await OpenApplicationAsync(command),
                "search_web" => await SearchWebAsync(command),
                "control_window" => await ControlWindowAsync(command),
                "system_command" => await SystemCommandAsync(command),
                _ => new ExecutionResult
                {
                    Success = false,
                    Message = $"Unknown action: {command.Action}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing action: {command.Action}");
            return new ExecutionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<ExecutionResult> OpenApplicationAsync(LLMResponse command)
    {
        var appName = command.Target.ToLowerInvariant();
        
        // Map common app names to executables
        var appExecutable = appName switch
        {
            "notepad" => "notepad.exe",
            "calculator" or "calc" => "calc.exe",
            "paint" => "mspaint.exe",
            "explorer" or "file explorer" => "explorer.exe",
            "chrome" => "chrome.exe",
            "edge" => "msedge.exe",
            "firefox" => "firefox.exe",
            "vscode" or "code" => "code.exe",
            "cmd" or "command prompt" => "cmd.exe",
            "powershell" => "powershell.exe",
            _ => appName.EndsWith(".exe") ? appName : $"{appName}.exe"
        };

        // Check guardrails
        if (!_guardRail.IsActionAllowed("open", appExecutable))
        {
            _logger.LogWarning($"Action blocked by guardrails: open {appExecutable}");
            return new ExecutionResult
            {
                Success = false,
                Message = $"Opening {appExecutable} is not allowed by security policy"
            };
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = appExecutable,
                UseShellExecute = true
            };

            // Add arguments if provided
            if (command.Parameters?.ContainsKey("args") == true)
            {
                startInfo.Arguments = command.Parameters["args"]?.ToString() ?? "";
            }

            Process.Start(startInfo);
            _logger.LogInformation($"Successfully opened: {appExecutable}");

            return new ExecutionResult
            {
                Success = true,
                Message = $"Opened {command.Target}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to open: {appExecutable}");
            return new ExecutionResult
            {
                Success = false,
                Message = $"Could not open {command.Target}: {ex.Message}"
            };
        }
    }

    private async Task<ExecutionResult> SearchWebAsync(LLMResponse command)
    {
        try
        {
            var query = command.Target;
            var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";

            var startInfo = new ProcessStartInfo
            {
                FileName = searchUrl,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            _logger.LogInformation($"Opened web search: {query}");

            return new ExecutionResult
            {
                Success = true,
                Message = $"Searching for: {query}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to search web: {command.Target}");
            return new ExecutionResult
            {
                Success = false,
                Message = $"Could not search web: {ex.Message}"
            };
        }
    }

    private async Task<ExecutionResult> ControlWindowAsync(LLMResponse command)
    {
        // TODO: Implement window control (close, minimize, maximize)
        // This will require Win32 API calls or similar
        _logger.LogWarning("Window control not yet implemented");
        return new ExecutionResult
        {
            Success = false,
            Message = "Window control feature coming soon"
        };
    }

    private async Task<ExecutionResult> SystemCommandAsync(LLMResponse command)
    {
        // TODO: Implement system commands (volume, brightness, etc.)
        _logger.LogWarning("System commands not yet implemented");
        return new ExecutionResult
        {
            Success = false,
            Message = "System commands feature coming soon"
        };
    }
}
