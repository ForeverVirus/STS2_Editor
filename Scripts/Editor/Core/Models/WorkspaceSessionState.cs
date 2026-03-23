namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class WorkspaceSessionState
{
    public string CurrentProjectPath { get; set; } = string.Empty;

    public string CurrentProjectDisplayName { get; set; } = string.Empty;

    public bool IsPackageMode { get; set; }

    public ModStudioEntityKind CurrentEntityKind { get; set; } = ModStudioEntityKind.Character;

    public string CurrentEntityId { get; set; } = string.Empty;

    public string CurrentCenterTab { get; set; } = "basic";

    public bool IsDirty { get; set; }
}
