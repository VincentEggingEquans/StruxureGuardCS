namespace StruxureGuard.Core.Tools.Infrastructure;

public interface ITool
{
    string ToolKey { get; }

    ValidationResult Validate(ToolRunContext ctx);

    Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct);
}
