namespace FileHustle.Data;

public static class ByteFormat
{
    private static readonly string[] Units = { "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        double size = bytes;
        var unitIndex = -1;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return unitIndex < 0 ? $"{bytes} bytes" : $"{size:0.0} {Units[unitIndex]}";
    }
}
