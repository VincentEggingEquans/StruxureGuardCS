namespace StruxureGuard.Core.Tools.AspPathChecker;

public static class AspPathCheckerParameterKeys
{
    public const string AspText = "AspText";
    public const string PathText = "PathText";

    // "build" or "check"
    public const string Mode = "Mode";

    // "true" or "false"
    public const string CheckAll = "CheckAll";

    // Comma-separated 0-based indices (only used if CheckAll=false)
    public const string SelectedIndices = "SelectedIndices";
}
