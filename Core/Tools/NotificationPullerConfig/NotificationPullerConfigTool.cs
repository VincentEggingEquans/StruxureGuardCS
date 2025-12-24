using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;


namespace StruxureGuard.Core.Tools.NotificationPullerConfig;

public sealed class NotificationPullerConfigTool : ITool
{
    public const string OutputKeyJson = "ConfigJson";
    public const string OutputKeyPath = "ConfigPath";

    public string ToolKey => Tools.ToolKeys.NotificationPullerConfig;
    public string DisplayName => "NotificationPuller Config";
    public string Description => "Generates a JSON config file for the notification puller agent (SSH).";

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        var ipsRaw = ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.IpsRaw) ?? "";
        if (string.IsNullOrWhiteSpace(ipsRaw))
            r.AddError("notif.ips", "Voer minimaal één ASP IP-adres in.");

        var pattern = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Pattern) ?? "").Trim();
        if (pattern.Length == 0)
            r.AddError("notif.pattern", "Voer een bestandsnaam-patroon in (bijv. * of *.xlsx).");

        var exportRoot = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.ExportRoot) ?? "").Trim();
        if (exportRoot.Length == 0)
            r.AddError("notif.exportRoot", "Voer een exportmap in (lokaal op agent).");

        var remoteDir = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.RemoteNotificationsDir) ?? "").Trim();
        if (remoteDir.Length == 0)
            r.AddError("notif.remoteDir", "Voer een remote notification directory in (op de ASP).");

        var saveMode = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.SaveMode) ?? "all").Trim().ToLowerInvariant();
        if (saveMode is not ("all" or "latest"))
            r.AddError("notif.saveMode", "Ongeldige opslagmodus. Kies 'all' of 'latest'.");

        var deleteCustom = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.DeleteCustom, false);
        var deleteCustomPattern = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.DeleteCustomPattern) ?? "").Trim();
        if (deleteCustom && deleteCustomPattern.Length == 0)
            r.AddError("notif.deleteCustomPattern", "Je hebt 'verwijder custom patroon' aangevinkt maar geen patroon ingevuld.");

        var port = ctx.Parameters.GetInt32(NotificationPullerConfigParameterKeys.SshPort);
        if (port is null || port <= 0)
            r.AddError("notif.sshPort", "SSH-port moet een geldig getal zijn (bijv. 22).");

        var cfgPath = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.ConfigPath) ?? "").Trim();
        if (cfgPath.Length == 0)
            r.AddError("notif.configPath", "Kies een pad voor het configbestand.");

        return r;
    }

    public async Task<ToolResult> RunAsync(ToolRunContext ctx, IProgress<ToolProgressInfo>? progress, CancellationToken ct)
    {
        var tag = "notif-puller";

        var started = DateTime.UtcNow;
        Log.Info(tag, $"tool-runner start runId={ctx.RunId.ToString("N")[..8]} params: {ctx.Parameters.ToLogString()}");

        try
        {
            progress?.Report(new ToolProgressInfo(0, 100, null, "Bouwen...", "build", 0, false));

            var ipsRaw = ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.IpsRaw) ?? "";
            var username = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Username) ?? "").Trim();

            var p1 = ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Password1) ?? "";
            var p2 = ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Password2) ?? "";
            var p3 = ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Password3) ?? "";

            var sshPort = ctx.Parameters.GetInt32(NotificationPullerConfigParameterKeys.SshPort) ?? 22;

            var remoteDir = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.RemoteNotificationsDir) ?? "").Trim();
            var pattern = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.Pattern) ?? "").Trim();
            var exportRoot = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.ExportRoot) ?? "").Trim();

            var makeZip = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.MakeZip, true);
            var saveMode = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.SaveMode) ?? "all").Trim().ToLowerInvariant();

            var deleteOh = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.DeleteOh, false);
            var deleteAssets = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.DeleteAssets, false);
            var deleteCustom = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.DeleteCustom, false);
            var deleteCustomPattern = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.DeleteCustomPattern) ?? "").Trim();
            var deleteAll = ctx.Parameters.GetBool(NotificationPullerConfigParameterKeys.DeleteAll, false);

            var cfgPath = (ctx.Parameters.GetString(NotificationPullerConfigParameterKeys.ConfigPath) ?? "").Trim();

            ct.ThrowIfCancellationRequested();

            var built = NotificationPullerConfigEngine.Build(
                ipsRaw: ipsRaw,
                username: username,
                password1Plain: p1,
                password2Plain: p2,
                password3Plain: p3,
                sshPort: sshPort,
                remoteDir: remoteDir,
                pattern: pattern,
                exportRoot: exportRoot,
                makeZip: makeZip,
                saveMode: saveMode,
                deleteOh: deleteOh,
                deleteAssets: deleteAssets,
                deleteCustom: deleteCustom,
                deleteCustomPattern: deleteCustomPattern,
                deleteAll: deleteAll);

            progress?.Report(new ToolProgressInfo(60, 100, null, "Schrijven...", "write", 60, false));

            // Write file (Core tool responsibility)
            var dir = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(cfgPath, built.Json, System.Text.Encoding.UTF8, ct);

            Log.Info(tag, $"write ok path='{cfgPath}' jsonBytes={built.Json.Length} ips={built.IpsParsedCount}");

            progress?.Report(new ToolProgressInfo(100, 100, null, "Klaar", "done", 100, false));

            var finished = DateTime.UtcNow;
            var ms = (long)(finished - started).TotalMilliseconds;

            return ToolResult.Ok($"Config opgeslagen: {cfgPath}", startedUtc: started, finishedUtc: finished)
                .WithOutput(OutputKeyPath, cfgPath)
                .WithOutput(OutputKeyJson, built.Json);
        }
        catch (OperationCanceledException)
        {
            var finished = DateTime.UtcNow;
            return ToolResult.CanceledResult("Geannuleerd.", startedUtc: started, finishedUtc: finished);
        }
        catch (Exception ex)
        {
            Log.Error(tag, $"EX: {ex.GetType().Name}: {ex.Message}\n{ex}");
            var finished = DateTime.UtcNow;
            return ToolResult.Fail("Fout bij genereren config.", startedUtc: started, finishedUtc: finished);
        }
        finally
        {
            Log.Info(tag, $"tool-runner finish runId={ctx.RunId.ToString("N")[..8]}");
        }
    }
}
