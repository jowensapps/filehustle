namespace FileHustle;

public static class BuildInfo
{
#if DEBUG
    public const bool IsDebug = true;
#else
    public const bool IsDebug = false;
#endif
}
