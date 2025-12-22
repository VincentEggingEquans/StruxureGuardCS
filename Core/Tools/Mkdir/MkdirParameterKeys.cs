namespace StruxureGuard.Core.Tools.Mkdir;

public static class MkdirParameterKeys
{
    // Required
    public const string BasePath = "BasePath";
    public const string FolderNames = "FolderNames"; // newline separated list

    // Optional / flags
    public const string CopyEnabled = "CopyEnabled";
    public const string SourceFilePath = "SourceFilePath";
    public const string CopyNamingMode = "CopyNamingMode"; // enum name or int

    public const string OverwriteExistingCopiedFiles = "OverwriteExistingCopiedFiles";
    public const string SkipCopyIfTargetExists = "SkipCopyIfTargetExists";
    public const string VerboseLogging = "VerboseLogging";

    // Snapshot-only (not required to run)
    public const string TotalNames = "TotalNames";
}
