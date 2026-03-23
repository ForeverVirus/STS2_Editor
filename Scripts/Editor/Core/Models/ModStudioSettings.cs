namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class ModStudioSettings
{
    public string UiLanguageCode { get; set; } = "zh-CN";

    public string LastProjectPath { get; set; } = string.Empty;

    public List<string> RecentProjectPaths { get; set; } = new();
}
