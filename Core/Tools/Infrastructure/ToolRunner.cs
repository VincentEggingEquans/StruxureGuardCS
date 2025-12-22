using System.Diagnostics;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Core.Tools.Infrastructure;

public static class ToolRunner
{
    public static async Task<ToolResult> RunAsync(
        ITool tool,
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        var startedUtc = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        Log.Info("tool-runner", $"START tool='{tool.ToolKey}' runId='{ctx.RunId}' startUtc='{startedUtc:O}'");

        ToolResult FinalizeResult(ToolResult result, DateTime finishedUtc)
        {
            // Always stamp telemetry and carry warnings
            var stamped = new ToolResult
            {
                Success = result.Success,
                Canceled = result.Canceled,
                Summary = result.Summary,
                StartedUtc = startedUtc,
                FinishedUtc = finishedUtc,
                DurationMs = sw.ElapsedMilliseconds
            }.WithWarnings(result.Warnings);

            if (stamped.Canceled)
                Log.Warn("tool-runner", $"CANCELED tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}'");
            else if (!stamped.Success)
                Log.Error("tool-runner", $"FAIL tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}'");
            else
                Log.Info("tool-runner", $"OK tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}'");

            if (stamped.Warnings.Count > 0)
                Log.Warn("tool-runner", $"WARNINGS tool='{tool.ToolKey}' runId='{ctx.RunId}' count={stamped.Warnings.Count}");

            return stamped;
        }

        try
        {
            var validation = tool.Validate(ctx);
            if (!validation.IsValid)
            {
                Log.Warn("tool-runner", $"BLOCKED tool='{tool.ToolKey}' runId='{ctx.RunId}' issues={validation.Issues.Count}");
                foreach (var iss in validation.Issues)
                    Log.Warn("tool-runner", $"  {iss.Severity} {iss.Code}: {iss.Message}");

                var blocked = new ToolResult
                {
                    Success = false,
                    Canceled = false,
                    Summary = "Validation failed"
                }.WithWarnings(validation.Warnings.Select(w => $"{w.Code}: {w.Message}"));

                return FinalizeResult(blocked, DateTime.UtcNow);
            }

            // Let the tool run. It can throw OperationCanceledException.
            var result = await tool.RunAsync(ctx, progress, ct).ConfigureAwait(false);

            return FinalizeResult(result, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancellation requested by our CTS/token
            var canceled = new ToolResult
            {
                Success = false,
                Canceled = true,
                Summary = "Canceled"
            };

            return FinalizeResult(canceled, DateTime.UtcNow);
        }
        catch (OperationCanceledException oce)
        {
            // Tool threw OCE without our token being canceled -> still mark as canceled, but log detail
            Log.Warn("tool-runner", $"OCE (not token-cancel) tool='{tool.ToolKey}' runId='{ctx.RunId}': {oce.Message}");

            var canceled = new ToolResult
            {
                Success = false,
                Canceled = true,
                Summary = "Canceled"
            };

            return FinalizeResult(canceled, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var failed = new ToolResult
            {
                Success = false,
                Canceled = false,
                Summary = $"{ex.GetType().Name}: {ex.Message}"
            };

            Log.Error("tool-runner", $"EXCEPTION tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={sw.ElapsedMilliseconds}: {ex.GetType().Name}: {ex.Message}\n{ex}");

            return FinalizeResult(failed, DateTime.UtcNow);
        }
        finally
        {
            sw.Stop();
        }
    }
}
