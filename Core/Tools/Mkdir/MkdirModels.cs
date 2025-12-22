namespace StruxureGuard.Tools.Mkdir;

public enum AutoNumberFormat
{
    None = 0,
    PrefixDash,   // 01 - Name
    PrefixDot,    // 1. Name
    SuffixSpace   // Name 01
}

public enum CopyNamingMode
{
    KeepOriginalName = 0,
    RenameToFolderName = 1
}

public sealed class MkdirOptions
{
    public required string BasePath { get; init; }
    public required List<string> FolderNames { get; init; }

    public bool CopyFileToEachFolder { get; init; }
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// Determines the filename used when copying into each created folder.
    /// </summary>
    public CopyNamingMode CopyNamingMode { get; init; } = CopyNamingMode.KeepOriginalName;

    public bool OverwriteExistingCopiedFiles { get; init; }
    public bool SkipCopyIfTargetExists { get; init; }

    public bool VerboseLogging { get; init; }

    public bool EnableAutoNumbering { get; init; }
    public AutoNumberFormat NumberFormat { get; init; } = AutoNumberFormat.None;
    public int NumberStart { get; init; } = 1;
    public int NumberPadding { get; init; } = 2;
}

public sealed class MkdirResult
{
    public int TotalRequested { get; init; }

    public int DirectoriesCreated { get; set; }
    public int DirectoriesAlreadyExisted { get; set; }

    public int FilesCopied { get; set; }
    public int FilesOverwritten { get; set; }
    public int FilesSkippedBecauseExists { get; set; }
    public int FailedItems { get; set; }
}

public readonly record struct MkdirProgress(int Done, int Total, string Current);
