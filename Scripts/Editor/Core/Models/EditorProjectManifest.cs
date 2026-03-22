namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EditorProjectManifest
{
    public string ProjectId { get; set; } = string.Empty;

    public string Name { get; set; } = "New Mod Studio Project";

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EditorVersion { get; set; } = "phase1";

    public string TargetGameVersion { get; set; } = "unknown";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
