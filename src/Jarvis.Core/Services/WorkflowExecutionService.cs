using Jarvis.Core.Interfaces;
using Jarvis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Services;

/// <summary>
/// Executes multi-step workflows with proper sequencing and error handling
/// </summary>
public class WorkflowExecutionService
{
    private readonly ILogger<WorkflowExecutionService> _logger;
    private readonly IGuardRailService _guardRail;
    private readonly IInputControlService? _inputControl;

    public WorkflowExecutionService(
        ILogger<WorkflowExecutionService> logger,
        IGuardRailService guardRail,
        IInputControlService? inputControl = null)
    {
        _logger = logger;
        _guardRail = guardRail;
        _inputControl = inputControl;
    }

    public async Task<ExecutionResult> ExecuteWorkflowAsync(LLMResponse response)
    {
        // Handle single-step commands
        if (!response.IsMultiStep)
        {
            var singleStep = await ExecuteSingleStepAsync(response);
            return new ExecutionResult
            {
                Success = singleStep.Success,
                Message = singleStep.Message,
                StepResults = new List<StepResult> { singleStep }
            };
        }

        // Execute multi-step workflow
        _logger.LogInformation($"Executing multi-step workflow with {response.Steps!.Count} steps");
        
        var stepResults = new List<StepResult>();
        var overallSuccess = true;

        foreach (var step in response.Steps!.OrderBy(s => s.Order))
        {
            _logger.LogInformation($"Step {step.Order}: {step.Action} → {step.Target}");

            // Execute the step
            var stepResult = await ExecuteStepAsync(step);
            stepResults.Add(stepResult);

            // If step failed, stop the workflow
            if (!stepResult.Success)
            {
                overallSuccess = false;
                _logger.LogWarning($"Step {step.Order} failed: {stepResult.Message}. Stopping workflow.");
                break;
            }

            // Apply delay if specified (e.g., wait for app to open)
            if (step.DelayMs > 0)
            {
                _logger.LogDebug($"Waiting {step.DelayMs}ms before next step");
                await Task.Delay(step.DelayMs);
            }
        }

        return new ExecutionResult
        {
            Success = overallSuccess,
            Message = overallSuccess 
                ? $"Completed all {stepResults.Count} steps successfully"
                : $"Workflow stopped at step {stepResults.Count} of {response.Steps.Count}",
            StepResults = stepResults
        };
    }

    private async Task<StepResult> ExecuteStepAsync(ActionStep step)
    {
        try
        {
            var result = step.Action.ToLowerInvariant() switch
            {
                "open_app" => await OpenApplicationAsync(step),
                "type_text" => await TypeTextAsync(step),
                "press_key" => await PressKeyAsync(step),
                "wait" => await WaitAsync(step),
                "search_web" => await SearchWebAsync(step),
                "click" => await ClickAsync(step),
                "move_mouse" => await MoveMouseAsync(step),
                _ => new StepResult
                {
                    StepNumber = step.Order,
                    Action = step.Action,
                    Success = false,
                    Message = $"Unknown action: {step.Action}"
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing step {step.Order}: {step.Action}");
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> ExecuteSingleStepAsync(LLMResponse response)
    {
        // Convert single action to step format
        var step = new ActionStep
        {
            Action = response.Action,
            Target = response.Target,
            Parameters = response.Parameters,
            Order = 1,
            Description = response.Message
        };

        return await ExecuteStepAsync(step);
    }

    private async Task<StepResult> OpenApplicationAsync(ActionStep step)
    {
        var appName = step.Target?.ToLowerInvariant() ?? "";
        
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

        if (!_guardRail.IsActionAllowed("open", appExecutable))
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Security policy blocks: {appExecutable}"
            };
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = appExecutable,
                UseShellExecute = true
            };

            if (step.Parameters?.ContainsKey("args") == true)
            {
                startInfo.Arguments = step.Parameters["args"]?.ToString() ?? "";
            }

            System.Diagnostics.Process.Start(startInfo);
            _logger.LogInformation($"Opened: {appExecutable}");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Opened {step.Target}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to open: {appExecutable}");
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Failed to open {step.Target}: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> TypeTextAsync(ActionStep step)
    {
        if (_inputControl == null)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = "Input control service not available"
            };
        }

        try
        {
            var text = step.Target ?? "";
            _inputControl.TypeText(text);
            _logger.LogInformation($"Typed text: {text}");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Typed: {text}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to type text");
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Failed to type text: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> PressKeyAsync(ActionStep step)
    {
        if (_inputControl == null)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = "Input control service not available"
            };
        }

        try
        {
            var key = step.Target ?? "";
            _inputControl.PressKey(key);
            _logger.LogInformation($"Pressed key: {key}");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Pressed: {key}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to press key: {step.Target}");
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Failed to press key: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> WaitAsync(ActionStep step)
    {
        try
        {
            var ms = 1000; // Default 1 second
            
            if (step.Parameters?.ContainsKey("milliseconds") == true)
            {
                ms = Convert.ToInt32(step.Parameters["milliseconds"]);
            }

            _logger.LogInformation($"Waiting {ms}ms");
            await Task.Delay(ms);

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Waited {ms}ms"
            };
        }
        catch (Exception ex)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Wait failed: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> SearchWebAsync(ActionStep step)
    {
        try
        {
            var query = step.Target ?? "";
            var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = searchUrl,
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(startInfo);
            _logger.LogInformation($"Opened web search: {query}");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Searching for: {query}"
            };
        }
        catch (Exception ex)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Search failed: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> ClickAsync(ActionStep step)
    {
        if (_inputControl == null)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = "Input control service not available"
            };
        }

        try
        {
            _inputControl.ClickMouse();
            _logger.LogInformation("Mouse clicked");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = "Mouse clicked"
            };
        }
        catch (Exception ex)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Click failed: {ex.Message}"
            };
        }
    }

    private async Task<StepResult> MoveMouseAsync(ActionStep step)
    {
        if (_inputControl == null)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = "Input control service not available"
            };
        }

        try
        {
            var x = step.Parameters?.ContainsKey("x") == true 
                ? Convert.ToInt32(step.Parameters["x"]) 
                : 0;
            var y = step.Parameters?.ContainsKey("y") == true 
                ? Convert.ToInt32(step.Parameters["y"]) 
                : 0;

            _inputControl.MoveMouse(x, y);
            _logger.LogInformation($"Mouse moved to ({x}, {y})");

            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = true,
                Message = $"Moved mouse to ({x}, {y})"
            };
        }
        catch (Exception ex)
        {
            return new StepResult
            {
                StepNumber = step.Order,
                Action = step.Action,
                Success = false,
                Message = $"Mouse move failed: {ex.Message}"
            };
        }
    }
}
