using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.Mkdir;

public sealed class MkdirTool : ITool
{
    public string ToolKey => ToolKeys.Mkdir;

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        try
        {
            var opt = BuildOptionsFrom(ctx.Parameters);
            MkdirEngine.Validate(opt);
        }
        catch (Exception ex)
        {
            r.AddError("mkdir.validate", ex.Message);
        }

        return r;
    }

    public async Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        Log.Info("mkdir", $"RunAsync via runner runId='{ctx.RunId}'");

        var opt = BuildOptionsFrom(ctx.Parameters);

        IProgress<MkdirProgress>? mkdirProgress = null;
        if (progress != null)
        {
            mkdirProgress = new Progress<MkdirProgress>(p =>
                progress.Report(new ToolProgressInfo(
                    Done: p.Done,
                    Total: p.Total,
                    CurrentItem: p.Current,
                    Message: null,
                    Phase: "Creating folders",
                    Percent: ToolProgressInfo.ComputePercent(p.Done, p.Total))));
        }

        var res = await MkdirEngine.ExecuteAsync(opt, mkdirProgress, ct);

        var summary =
            $"created={res.DirectoriesCreated}, existed={res.DirectoriesAlreadyExisted}, " +
            $"copied={res.FilesCopied}, overwritten={res.FilesOverwritten}, skipped={res.FilesSkippedBecauseExists}";

        return ToolResult.Ok(summary);
    }

    private static MkdirOptions BuildOptionsFrom(ToolParameters p)
    {
        var basePath = p.GetRequiredString(MkdirParameterKeys.BasePath).Trim();

        // FolderNames is stored as newline-separated list
        var folderNamesRaw = p.GetRequiredString(MkdirParameterKeys.FolderNames);

        var names = folderNamesRaw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var copyEnabled = p.GetBool(MkdirParameterKeys.CopyEnabled, defaultValue: false);
        var sourceFile = p.GetString(MkdirParameterKeys.SourceFilePath);
        if (string.IsNullOrWhiteSpace(sourceFile))
            sourceFile = null;

        var namingMode = CopyNamingMode.KeepOriginalName;
        var namingModeRaw = p.GetString(MkdirParameterKeys.CopyNamingMode);
        if (!string.IsNullOrWhiteSpace(namingModeRaw))
        {
            // Accept enum names or numeric values
            if (Enum.TryParse<CopyNamingMode>(namingModeRaw, ignoreCase: true, out var parsed))
                namingMode = parsed;
            else if (int.TryParse(namingModeRaw, out var i) && Enum.IsDefined(typeof(CopyNamingMode), i))
                namingMode = (CopyNamingMode)i;
        }

        var overwrite = p.GetBool(MkdirParameterKeys.OverwriteExistingCopiedFiles, defaultValue: copyEnabled);
        var skipIfExists = p.GetBool(MkdirParameterKeys.SkipCopyIfTargetExists, defaultValue: false);
        var verbose = p.GetBool(MkdirParameterKeys.VerboseLogging, defaultValue: false);

        return new MkdirOptions
        {
            BasePath = basePath,
            FolderNames = names,

            CopyFileToEachFolder = copyEnabled,
            SourceFilePath = copyEnabled ? sourceFile : null,
            CopyNamingMode = namingMode,

            OverwriteExistingCopiedFiles = overwrite,
            SkipCopyIfTargetExists = skipIfExists,

            VerboseLogging = verbose,

            // Not used by engine (yet), but keep explicit defaults
            EnableAutoNumbering = false
        };
    }
}
