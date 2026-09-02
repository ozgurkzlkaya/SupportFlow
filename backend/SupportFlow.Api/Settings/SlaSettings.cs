namespace SupportFlow.Api.Settings;

public sealed class SlaSettings
{
    public const string SectionName = "Sla";

    public SlaPriorityTargets Low { get; set; } = new(1440, 7200);
    public SlaPriorityTargets Normal { get; set; } = new(480, 2880);
    public SlaPriorityTargets High { get; set; } = new(120, 960);
    public SlaPriorityTargets Critical { get; set; } = new(30, 240);
}

public sealed class SlaPriorityTargets
{
    public SlaPriorityTargets()
    {
    }

    public SlaPriorityTargets(int firstResponseMinutes, int resolutionMinutes)
    {
        FirstResponseMinutes = firstResponseMinutes;
        ResolutionMinutes = resolutionMinutes;
    }

    public int FirstResponseMinutes { get; set; }
    public int ResolutionMinutes { get; set; }
}
