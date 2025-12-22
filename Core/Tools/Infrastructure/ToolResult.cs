namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed class ToolResult
{
    public const string ValidationFailedSummary = "Validation failed";

    public bool Success { get; init; }
    public bool Canceled { get; init; }

    /// <summary>
    /// True when the run did not start because Validate() returned errors.
    /// </summary>
    public bool BlockedByValidation { get; init; }

    public string Summary { get; init; } = "";

    // Warnings (non-fatal issues)
    public List<string> Warnings { get; } = new();

    // Outputs (payload for UI / post-run actions)
    public Dictionary<string, string> Outputs { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Timing / telemetry
    public DateTime StartedUtc { get; init; }
    public DateTime FinishedUtc { get; init; }
    public long DurationMs { get; init; }

    public static ToolResult Ok(string summary = "OK", DateTime? startedUtc = null, DateTime? finishedUtc = null) =>
        new()
        {
            Success = true,
            Canceled = false,
            BlockedByValidation = false,
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
            BlockedByValidation = false,
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
            BlockedByValidation = false,
            Summary = summary,
            StartedUtc = startedUtc ?? DateTime.UtcNow,
            FinishedUtc = finishedUtc ?? DateTime.UtcNow,
            DurationMs = (long)((finishedUtc ?? DateTime.UtcNow) - (startedUtc ?? DateTime.UtcNow)).TotalMilliseconds
        };

    /// <summary>
    /// Canonical result for validation-blocked runs.
    /// </summary>
    public static ToolResult ValidationBlocked(IEnumerable<string> warnings)
    {
        return new ToolResult
        {
            Success = false,
            Canceled = false,
            BlockedByValidation = true,
            Summary = ValidationFailedSummary,
            StartedUtc = DateTime.UtcNow,
            FinishedUtc = DateTime.UtcNow,
            DurationMs = 0
        }.WithWarnings(warnings);
    }

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

    public ToolResult WithOutput(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            Outputs[key] = value ?? "";
        return this;
    }

    public ToolResult WithOutputs(IEnumerable<KeyValuePair<string, string>> outputs)
    {
        foreach (var kv in outputs)
            WithOutput(kv.Key, kv.Value);
        return this;
    }

    public string? TryGetOutput(string key)
        => Outputs.TryGetValue(key, out var v) ? v : null;
}
