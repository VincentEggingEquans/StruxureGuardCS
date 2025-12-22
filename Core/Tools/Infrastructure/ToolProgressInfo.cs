namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed record ToolProgressInfo(
    int Done,
    int Total,
    string? CurrentItem,
    string? Message,
    string? Phase = null,
    int? Percent = null,
    bool IsIndeterminate = false)
{
    public static ToolProgressInfo Indeterminate(string? message = null, string? phase = null)
        => new(0, 0, null, message, phase, null, true);

    public static int? ComputePercent(int done, int total)
    {
        if (total <= 0) return null;
        var pct = (int)Math.Round(done * 100.0 / total);
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        return pct;
    }
}
