using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools;

namespace StruxureGuard.Core.Tools.Mkdir;

public sealed class MkdirTool : ITool
{
    private readonly MkdirOptions _opt;

    public MkdirTool(MkdirOptions opt)
    {
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
    }

    public string ToolKey => ToolKeys.Mkdir;


    // ✅ New canonical validation for ToolRunner
    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        try
        {
            MkdirEngine.Validate(_opt);
        }
        catch (Exception ex)
        {
            // We keep it simple: engine throws -> tool validation error.
            // You can later map to structured codes if you want.
            r.AddError("mkdir.validate", ex.Message);
        }

        return r;
    }

    public async Task<ToolResult> RunAsync(
        ToolRunContext context,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        Log.Info("mkdir", $"RunAsync via runner runId='{context.RunId}'");

        IProgress<MkdirProgress>? mkdirProgress = null;
        if (progress != null)
        {
            mkdirProgress = new Progress<MkdirProgress>(p =>
                progress.Report(new ToolProgressInfo(
                    p.Done,
                    p.Total,
                    p.Current,
                    null,
                    Phase: "Creating folders",
                    Percent: ToolProgressInfo.ComputePercent(p.Done, p.Total))));
        }


        var res = await MkdirEngine.ExecuteAsync(_opt, mkdirProgress, cancellationToken);

        var summary =
            $"created={res.DirectoriesCreated}, existed={res.DirectoriesAlreadyExisted}, " +
            $"copied={res.FilesCopied}, overwritten={res.FilesOverwritten}, skipped={res.FilesSkippedBecauseExists}";

        return ToolResult.Ok(summary);
    }
}
