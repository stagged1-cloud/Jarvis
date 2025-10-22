using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Services;

/// <summary>
/// GuardRail service for security and action approval
/// </summary>
public class GuardRailService : IGuardRailService
{
    private readonly ILogger<GuardRailService> _logger;
    private readonly HashSet<string> _allowedApps;
    private readonly HashSet<string> _allowedDomains;
    private readonly List<ActionLog> _actionLogs;
    private readonly bool _requireApproval;

    public GuardRailService(ILogger<GuardRailService> logger, Models.SecurityConfig? config = null)
    {
        _logger = logger;
        _actionLogs = new List<ActionLog>();
        
        config ??= new Models.SecurityConfig();
        
        _allowedApps = new HashSet<string>(config.AllowedApps, StringComparer.OrdinalIgnoreCase);
        _allowedDomains = new HashSet<string>(config.AllowedDomains, StringComparer.OrdinalIgnoreCase);
        _requireApproval = config.RequireApproval;

        _logger.LogInformation($"GuardRail initialized. Allowed apps: {_allowedApps.Count}, Require approval: {_requireApproval}");
    }

    public bool IsActionAllowed(string action, string target)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            _logger.LogWarning("Empty action requested");
            return false;
        }

        // Check if target app is in allowlist
        if (!string.IsNullOrWhiteSpace(target))
        {
            var targetLower = target.ToLowerInvariant();
            
            // Check for executable name
            if (targetLower.EndsWith(".exe"))
            {
                if (_allowedApps.Count > 0 && !_allowedApps.Contains(targetLower))
                {
                    _logger.LogWarning($"Action '{action}' denied - target '{target}' not in allowed apps");
                    return false;
                }
            }
            
            // Check for URLs/domains
            if (targetLower.StartsWith("http://") || targetLower.StartsWith("https://"))
            {
                var domain = ExtractDomain(target);
                if (_allowedDomains.Count > 0 && !_allowedDomains.Contains(domain))
                {
                    _logger.LogWarning($"Action '{action}' denied - domain '{domain}' not in allowed domains");
                    return false;
                }
            }
        }

        // Always allow safe actions
        var safeActions = new[] { "speak", "listen", "display", "notify" };
        if (safeActions.Contains(action.ToLowerInvariant()))
        {
            return true;
        }

        // Dangerous actions require approval
        var dangerousActions = new[] { "delete", "shutdown", "restart", "install", "uninstall" };
        if (dangerousActions.Contains(action.ToLowerInvariant()))
        {
            _logger.LogWarning($"Dangerous action '{action}' requires explicit approval");
            return false; // UI must handle approval separately
        }

        return true;
    }

    public void LogAction(string action, bool approved)
    {
        var log = new ActionLog
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Approved = approved
        };

        _actionLogs.Add(log);
        
        var status = approved ? "APPROVED" : "DENIED";
        _logger.LogInformation($"Action {status}: {action} at {log.Timestamp:yyyy-MM-dd HH:mm:ss}");
    }

    public IReadOnlyList<ActionLog> GetActionLogs(int count = 100)
    {
        return _actionLogs.TakeLast(count).ToList();
    }

    public void ClearLogs()
    {
        _actionLogs.Clear();
        _logger.LogInformation("Action logs cleared");
    }

    private string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    public class ActionLog
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public bool Approved { get; set; }
    }
}
