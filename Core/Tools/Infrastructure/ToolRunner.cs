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

        var paramLog = ctx.Parameters.ToLogString();
        Log.Info(ToolLogTags.Runner,
            $"START tool='{tool.ToolKey}' runId='{ctx.RunId}' startUtc='{startedUtc:O}' paramsCount={ctx.Parameters.Count}" +
            (string.IsNullOrWhiteSpace(paramLog) ? "" : $" params={paramLog}"));

        ToolResult FinalizeResult(ToolResult result, DateTime finishedUtc)
        {
            var stamped = new ToolResult
            {
                Success = result.Success,
                Canceled = result.Canceled,
                BlockedByValidation = result.BlockedByValidation,
                Summary = result.Summary,
                StartedUtc = startedUtc,
                FinishedUtc = finishedUtc,
                DurationMs = sw.ElapsedMilliseconds
            }
            .WithWarnings(result.Warnings)
            .WithOutputs(result.Outputs);

            var warningsCount = stamped.Warnings.Count;
            var outputsCount = stamped.Outputs.Count;

            if (stamped.Canceled)
                Log.Warn(ToolLogTags.Runner,
                    $"CANCELED tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}' warnings={warningsCount} outputs={outputsCount}");
            else if (!stamped.Success)
                Log.Error(ToolLogTags.Runner,
                    $"FAIL tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}' warnings={warningsCount} outputs={outputsCount}");
            else
                Log.Info(ToolLogTags.Runner,
                    $"OK tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={stamped.DurationMs} summary='{stamped.Summary}' warnings={warningsCount} outputs={outputsCount}");

            if (warningsCount > 0)
                Log.Warn(ToolLogTags.Runner,
                    $"WARNINGS tool='{tool.ToolKey}' runId='{ctx.RunId}' count={warningsCount}");

            return stamped;
        }

        try
        {
            var validation = tool.Validate(ctx);
            if (!validation.IsValidEx())
            {
                Log.Warn(ToolLogTags.Runner,
                    $"BLOCKED tool='{tool.ToolKey}' runId='{ctx.RunId}' {validation.ToLogString()}" +
                    (string.IsNullOrWhiteSpace(paramLog) ? "" : $" params={paramLog}"));

                foreach (var line in validation.ToLogLines())
                    Log.Warn(ToolLogTags.Runner, "  - " + line);

                var blocked = ToolResult.ValidationBlocked(
                    validation.Warnings.Select(w => $"{w.Code}: {w.Message}"));

                return FinalizeResult(blocked, DateTime.UtcNow);
            }

            var result = await tool.RunAsync(ctx, progress, ct);
            return FinalizeResult(result, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            Log.Warn(ToolLogTags.Runner,
                $"CANCEL tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={sw.ElapsedMilliseconds} paramsCount={ctx.Parameters.Count}" +
                (string.IsNullOrWhiteSpace(paramLog) ? "" : $" params={paramLog}"));

            var canceled = ToolResult.CanceledResult("Canceled");
            return FinalizeResult(canceled, DateTime.UtcNow);
        }

        catch (Exception ex)
        {
            var failed = ToolResult.Fail($"Exception: {ex.GetType().Name}: {ex.Message}");

            Log.Error(ToolLogTags.Runner,
                $"EXCEPTION tool='{tool.ToolKey}' runId='{ctx.RunId}' ms={sw.ElapsedMilliseconds} paramsCount={ctx.Parameters.Count}" +
                (string.IsNullOrWhiteSpace(paramLog) ? "" : $" params={paramLog}") +
                $": {ex.GetType().Name}: {ex.Message}\n{ex}");

            return FinalizeResult(failed, DateTime.UtcNow);
        }
        finally
        {
            sw.Stop();
        }
    }
}
