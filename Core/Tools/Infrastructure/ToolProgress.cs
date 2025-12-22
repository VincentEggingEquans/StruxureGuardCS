namespace StruxureGuard.Core.Tools.Infrastructure;

public static class ToolProgress
{
    public static void Report(
        IProgress<ToolProgressInfo>? progress,
        int done,
        int total,
        string? currentItem = null,
        string? message = null,
        string? phase = null)
    {
        if (progress == null) return;

        progress.Report(new ToolProgressInfo(
            Done: done,
            Total: total,
            CurrentItem: currentItem,
            Message: message,
            Phase: phase,
            Percent: ToolProgressInfo.ComputePercent(done, total),
            IsIndeterminate: false));
    }

    public static void ReportIndeterminate(
        IProgress<ToolProgressInfo>? progress,
        string? message = null,
        string? phase = null)
    {
        if (progress == null) return;
        progress.Report(ToolProgressInfo.Indeterminate(message, phase));
    }
}
