namespace STS2_Editor.Scripts.Editor.Packaging;

internal static class PackagingPathUtility
{
    public static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    public static string NormalizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
            {
                chars[i] = '_';
            }
        }

        string normalized = new(chars);
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim('_', '.', ' ');
    }

    public static string EnsureArchiveExtension(string path)
    {
        return Path.GetExtension(path).Equals(".sts2pack", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{path}.sts2pack";
    }
}
