using System.Diagnostics;
using System.Text;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.NotificationPullerAgentPublisher;

public sealed class NotificationPullerAgentPublisherTool : ITool
{
    public const string OutputKeyPublishDir = "publish_dir";
    public const string OutputKeyCopiedConfigPath = "copied_config_path";

    public string ToolKey => ToolKeys.NotificationPullerAgentPublisher;

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var v = new ValidationResult();
        var p = ctx.Parameters;

        var outDir = (p.GetString(NotificationPullerAgentPublisherParameterKeys.OutputFolder) ?? "").Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            v.AddError("output.required", "Kies een output map voor de agent build.");

        var csproj = ResolveAgentCsprojPath();
        if (csproj is null || !File.Exists(csproj))
            v.AddError("agent.csproj.missing", "Agent csproj niet gevonden. Verwacht: Agent\\NotificationPuller\\StruxureGuard.Agent.NotificationPuller.csproj");

        var copyCfg = p.GetBool(NotificationPullerAgentPublisherParameterKeys.CopyConfig, true);
        if (copyCfg)
        {
            var cfg = (p.GetString(NotificationPullerAgentPublisherParameterKeys.ConfigPath) ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cfg))
                v.AddError("config.required", "Configpad is leeg, maar 'copy config' staat aan.");
            else if (!File.Exists(cfg))
                v.AddError("config.missing", $"Configbestand bestaat niet: {cfg}");
        }

        return v;
    }

    public async Task<ToolResult> RunAsync(ToolRunContext ctx, IProgress<ToolProgressInfo>? progress, CancellationToken ct)
    {
        const string tag = "agent";

        var p = ctx.Parameters;

        var outDir = (p.GetString(NotificationPullerAgentPublisherParameterKeys.OutputFolder) ?? "").Trim();
        var cfgPath = (p.GetString(NotificationPullerAgentPublisherParameterKeys.ConfigPath) ?? "").Trim();
        var copyCfg = p.GetBool(NotificationPullerAgentPublisherParameterKeys.CopyConfig, true);

        var runtime = (p.GetString(NotificationPullerAgentPublisherParameterKeys.Runtime) ?? "win-x64").Trim();
        var configuration = (p.GetString(NotificationPullerAgentPublisherParameterKeys.Configuration) ?? "Release").Trim();
        var selfContained = p.GetBool(NotificationPullerAgentPublisherParameterKeys.SelfContained, true);
        var singleFile = p.GetBool(NotificationPullerAgentPublisherParameterKeys.SingleFile, true);

        var csproj = ResolveAgentCsprojPath();
        if (csproj is null || !File.Exists(csproj))
            return ToolResult.Fail("Agent project niet gevonden.");

        Directory.CreateDirectory(outDir);

        Log.Info(tag, $"Publish start. runId='{ctx.RunId}' csproj='{csproj}' outDir='{outDir}' cfg='{configuration}' rt='{runtime}' selfContained={selfContained} singleFile={singleFile}");

        progress?.Report(ToolProgressInfo.Indeterminate("dotnet publish gestart...", phase: "publish"));

        var args = new StringBuilder();
        args.Append("publish ");
        args.Append('"').Append(csproj).Append("\" ");
        args.Append("-c ").Append(configuration).Append(' ');
        args.Append("-r ").Append(runtime).Append(' ');
        args.Append("--self-contained ").Append(selfContained ? "true " : "false ");
        if (singleFile)
            args.Append("/p:PublishSingleFile=true ");
        args.Append("/p:DebugType=None /p:DebugSymbols=false ");
        args.Append("-o ").Append('"').Append(outDir).Append('"');

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args.ToString(),
            WorkingDirectory = Path.GetDirectoryName(csproj)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var (exitCode, lastLines) = await RunProcessAndLogAsync(psi, tag, progress, ct).ConfigureAwait(false);
        if (exitCode != 0)
        {
            Log.Error(tag, $"Publish FAILED exitCode={exitCode}. LastOutput='{lastLines}'");
            return ToolResult.Fail($"Agent publish faalde (exitCode={exitCode}). Zie log (Alt+L).");
        }

        string? copiedCfg = null;
        if (copyCfg && !string.IsNullOrWhiteSpace(cfgPath) && File.Exists(cfgPath))
        {
            try
            {
                copiedCfg = Path.Combine(outDir, Path.GetFileName(cfgPath));
                File.Copy(cfgPath, copiedCfg, overwrite: true);
                Log.Info(tag, $"Config copied -> '{copiedCfg}'");
            }
            catch (Exception ex)
            {
                Log.Warn(tag, $"Config copy failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        progress?.Report(ToolProgressInfo.Indeterminate("Publish klaar.", phase: "done"));

        var ok = ToolResult.Ok($"Agent gepubliceerd naar: {outDir}")
            .WithOutput(OutputKeyPublishDir, outDir);

        if (!string.IsNullOrWhiteSpace(copiedCfg))
            ok.WithOutput(OutputKeyCopiedConfigPath, copiedCfg);

        return ok;
    }

    private static string? ResolveAgentCsprojPath()
    {
        // Zoek repo root vanaf app base dir omhoog (handig als je vanuit bin\Debug draait)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Agent", "NotificationPuller", "StruxureGuard.Agent.NotificationPuller.csproj");

            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        // fallback: current directory (als je vanuit repo runt)
        var cwdCandidate = Path.Combine(
            Environment.CurrentDirectory,
            "Agent", "NotificationPuller", "StruxureGuard.Agent.NotificationPuller.csproj");

        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }

    private static async Task<(int ExitCode, string LastLines)> RunProcessAndLogAsync(
        ProcessStartInfo psi,
        string tag,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        var last = new Queue<string>(capacity: 10);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void Track(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            if (last.Count == 10) last.Dequeue();
            last.Enqueue(line);

            Log.Debug(tag, $"dotnet: {line}");
            progress?.Report(ToolProgressInfo.Indeterminate(line, phase: "publish"));
        }

        proc.Start();

        using var _ = ct.Register(() =>
        {
            try
            {
                if (!proc.HasExited)
                {
                    Log.Warn(tag, "Cancel requested -> killing dotnet process");
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { /* ignore */ }
        });

        var stdOut = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is null) break;
                Track(line);
            }
        }, ct);

        var stdErr = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line is null) break;
                Track(line);
            }
        }, ct);

        await Task.WhenAll(stdOut, stdErr).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        return (proc.ExitCode, string.Join(" | ", last));
    }
}
