using Microsoft.Extensions.Options;

namespace SupportFlow.Api.Settings;

public sealed class SlaSettingsValidator : IValidateOptions<SlaSettings>
{
    public ValidateOptionsResult Validate(string? name, SlaSettings settings)
    {
        var failures = new List<string>();
        ValidateTargets(nameof(settings.Low), settings.Low, failures);
        ValidateTargets(nameof(settings.Normal), settings.Normal, failures);
        ValidateTargets(nameof(settings.High), settings.High, failures);
        ValidateTargets(nameof(settings.Critical), settings.Critical, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTargets(
        string priority,
        SlaPriorityTargets? targets,
        ICollection<string> failures)
    {
        if (targets is null)
        {
            failures.Add($"Sla:{priority} configuration is required.");
            return;
        }

        if (targets.FirstResponseMinutes <= 0)
        {
            failures.Add($"Sla:{priority}:FirstResponseMinutes must be greater than zero.");
        }

        if (targets.ResolutionMinutes <= 0)
        {
            failures.Add($"Sla:{priority}:ResolutionMinutes must be greater than zero.");
        }

        if (targets.ResolutionMinutes < targets.FirstResponseMinutes)
        {
            failures.Add($"Sla:{priority}:ResolutionMinutes must be greater than or equal to FirstResponseMinutes.");
        }
    }
}
