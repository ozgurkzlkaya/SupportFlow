namespace SupportFlow.Api.Settings;

public class QueueAgingSettings
{
    public const string SectionName = "QueueAging";

    // Deliberate rollout switch; SQL failures are never silently redirected at runtime.
    public bool SqlProjectionEnabled { get; set; } = true;
}
