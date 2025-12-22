namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed class ToolResult
{
    public bool Success { get; init; }
    public bool Canceled { get; init; }
    public string Summary { get; init; } = "";

    // New: warnings (non-fatal issues)
    public List<string> Warnings { get; } = new();

    // New: timing / telemetry
    public DateTime StartedUtc { get; init; }
    public DateTime FinishedUtc { get; init; }
    public long DurationMs { get; init; }

    public static ToolResult Ok(string summary = "OK", DateTime? startedUtc = null, DateTime? finishedUtc = null) =>
        new()
        {
            Success = true,
            Canceled = false,
            Summary = summary,
            StartedUtc = startedUtc ?? DateTime.UtcNow,
            FinishedUtc = finishedUtc ?? DateTime.UtcNow,
            DurationMs = (long)((finishedUtc ?? DateTime.UtcNow) - (startedUtc ?? DateTime.UtcNow)).TotalMilliseconds
        };

    public static ToolResult CanceledResult(string summary = "Canceled", DateTime? startedUtc = null, DateTime? finishedUtc = null) =>
        new()
        {
            Success = false,
            Canceled = true,
            Summary = summary,
            StartedUtc = startedUtc ?? DateTime.UtcNow,
            FinishedUtc = finishedUtc ?? DateTime.UtcNow,
            DurationMs = (long)((finishedUtc ?? DateTime.UtcNow) - (startedUtc ?? DateTime.UtcNow)).TotalMilliseconds
        };

    public static ToolResult Fail(string summary, DateTime? startedUtc = null, DateTime? finishedUtc = null) =>
        new()
        {
            Success = false,
            Canceled = false,
            Summary = summary,
            StartedUtc = startedUtc ?? DateTime.UtcNow,
            FinishedUtc = finishedUtc ?? DateTime.UtcNow,
            DurationMs = (long)((finishedUtc ?? DateTime.UtcNow) - (startedUtc ?? DateTime.UtcNow)).TotalMilliseconds
        };

    public ToolResult WithWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
            Warnings.Add(warning);
        return this;
    }

    public ToolResult WithWarnings(IEnumerable<string> warnings)
    {
        foreach (var w in warnings)
            WithWarning(w);
        return this;
    }
}
