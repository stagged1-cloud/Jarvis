using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Services;

/// <summary>
/// Manages and coordinates skills (plugins)
/// </summary>
public class SkillManager : ISkillManager
{
    private readonly Dictionary<string, ISkill> _skills;
    private readonly ILogger<SkillManager> _logger;

    public SkillManager(ILogger<SkillManager> logger)
    {
        _logger = logger;
        _skills = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);
    }

    public ISkill? GetSkill(string name)
    {
        if (_skills.TryGetValue(name, out var skill))
        {
            _logger.LogDebug($"Found skill: {name}");
            return skill;
        }

        _logger.LogWarning($"Skill not found: {name}");
        return null;
    }

    public void RegisterSkill(ISkill skill)
    {
        if (skill == null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        if (string.IsNullOrWhiteSpace(skill.Name))
        {
            throw new ArgumentException("Skill name cannot be empty", nameof(skill));
        }

        _skills[skill.Name] = skill;
        _logger.LogInformation($"Registered skill: {skill.Name} - {skill.Description}");
    }

    public void UnregisterSkill(string name)
    {
        if (_skills.Remove(name))
        {
            _logger.LogInformation($"Unregistered skill: {name}");
        }
        else
        {
            _logger.LogWarning($"Attempted to unregister non-existent skill: {name}");
        }
    }

    public IReadOnlyList<ISkill> GetAllSkills()
    {
        return _skills.Values.ToList();
    }

    public bool HasSkill(string name)
    {
        return _skills.ContainsKey(name);
    }
}
