using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Styling;

namespace StruxureGuard.UI.Tools;

public abstract class ToolBaseForm : Form
{
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    protected bool IsRunning => _isRunning;

    protected ToolBaseForm()
    {
        Load += (_, __) => ThemeManager.ApplyTheme(this);
    }

    protected abstract void UpdateUiRunningState(bool isRunning);
    protected virtual void OnProgress(ToolProgressInfo p) { }

    protected void CancelRun()
    {
        if (!_isRunning) return;
        Log.Info("tool-ui", $"{GetType().Name} cancel requested");
        try { _cts?.Cancel(); } catch { }
    }

    protected async Task<ToolResult> RunToolAsync(
        ITool tool,
        Dictionary<string, string> parameters,
        string toolLogTag,
        Func<ToolResult, Task>? onCompleted = null,
        bool showWarningsOnSuccess = false,
        Func<ToolResult, string?>? successMessageFactory = null)
    {
        if (_isRunning)
            return ToolResult.Fail("Already running");

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _isRunning = true;
        UpdateUiRunningState(true);

        var ctx = new ToolRunContext(toolKey: tool.ToolKey, parameters: parameters);


        var uiProgress = new Progress<ToolProgressInfo>(p => OnProgress(p));
        using var throttled = new ThrottledProgressReporter<ToolProgressInfo>(uiProgress, TimeSpan.FromMilliseconds(75));

        ToolResult result;
        try
        {
            result = await ToolRunner.RunAsync(tool, ctx, throttled, ct);
            throttled.Flush();
        }
        finally
        {
            _isRunning = false;
            UpdateUiRunningState(false);

            _cts?.Dispose();
            _cts = null;
        }

        if (onCompleted != null)
            await onCompleted(result);

        // ✅ Centralized result handling (optional, but default is useful)
        await HandleToolResultAsync(
            toolLogTag: toolLogTag,
            result: result,
            showWarningsOnSuccess: showWarningsOnSuccess,
            successMessageFactory: successMessageFactory);

        return result;
    }

    /// <summary>
    /// Centralized UX for tool results (Canceled / Validation failed / Fail / Success + optional warnings)
    /// Override if a specific tool needs custom messaging.
    /// </summary>
    protected virtual Task HandleToolResultAsync(
        string toolLogTag,
        ToolResult result,
        bool showWarningsOnSuccess,
        Func<ToolResult, string?>? successMessageFactory)
    {
        if (result.Canceled)
        {
            Log.Warn(toolLogTag, $"Tool canceled. ms={result.DurationMs}");
            MessageBox.Show(this, "Geannuleerd.", "Tool", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        if (!result.Success)
        {
            if (result.BlockedByValidation)
            {
                Log.Warn(toolLogTag, $"Tool blocked by validation. ms={result.DurationMs}");
                MessageBox.Show(this,
                    "Kan niet starten omdat invoer ongeldig is.\r\n\r\nZie log (Alt+L) voor details.",
                    "Validatie",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return Task.CompletedTask;
            }

            Log.Error(toolLogTag, $"Tool failed. ms={result.DurationMs}. Summary='{result.Summary}'");
            MessageBox.Show(this,
                "Er ging iets mis. Kijk in de log (Alt+L).",
                "Fout",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return Task.CompletedTask;
        }

        // Success
        Log.Info(toolLogTag, $"Tool succeeded. ms={result.DurationMs}. Summary='{result.Summary}'");

        if (showWarningsOnSuccess && result.Warnings.Count > 0)
        {
            Log.Warn(toolLogTag, $"Tool succeeded with warnings: {result.Warnings.Count}");
            var warnText = string.Join(Environment.NewLine, result.Warnings.Select(w => "• " + w));
            MessageBox.Show(this,
                "Klaar, maar met waarschuwingen:\r\n\r\n" + warnText,
                "Waarschuwingen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        var successMsg = successMessageFactory?.Invoke(result) ?? "Klaar.";
        MessageBox.Show(this, successMsg, "Klaar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return Task.CompletedTask;
    }
}
