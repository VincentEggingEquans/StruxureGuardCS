using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Core.Tools.NotificationPullerConfig;

public static class NotificationPullerConfigEngine
{
    // LET OP: moet matchen met Notificationpulleragent.
    private static readonly byte[] SecretKey = Encoding.ASCII.GetBytes("StruxureGuardNotificationSecret");

    public static NotificationPullerConfigBuildResult Build(
        string ipsRaw,
        string username,
        string password1Plain,
        string password2Plain,
        string password3Plain,
        int sshPort,
        string remoteDir,
        string pattern,
        string exportRoot,
        bool makeZip,
        string saveMode,
        bool deleteOh,
        bool deleteAssets,
        bool deleteCustom,
        string deleteCustomPattern,
        bool deleteAll)
    {
        var ipsLinesIn = CountNonNullLines(ipsRaw);
        var ips = ParseIps(ipsRaw, out var dupSkipped);

        Log.Info("notif-puller", $"engine: ips linesIn={ipsLinesIn} parsed={ips.Count} dupSkipped={dupSkipped}");

        // Encrypt passwords (xor+base64 like Python)
        var p1 = EncryptPassword(password1Plain);
        var p2 = EncryptPassword(password2Plain);
        var p3 = EncryptPassword(password3Plain);

        var cfg = new NotificationPullerConfigFile
        {
            Ips = ips,
            Username = username ?? "",
            PasswordEnc = p1,
            SecondaryPasswordEnc = p2,
            TertiaryPasswordEnc = p3,

            SshPort = sshPort,
            RemoteNotificationsDir = remoteDir ?? "",

            Pattern = pattern ?? "",
            ExportRoot = exportRoot ?? "",
            MakeZip = makeZip,
            SaveMode = saveMode ?? "all",

            DeleteOh = deleteOh,
            DeleteAssets = deleteAssets,
            DeleteCustom = deleteCustom,
            DeleteCustomPattern = deleteCustomPattern ?? "",
            DeleteAll = deleteAll
        };

        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });

        return new NotificationPullerConfigBuildResult(
            Config: cfg,
            Json: json,
            IpsInputLines: ipsLinesIn,
            IpsParsedCount: ips.Count,
            IpsDuplicatesSkipped: dupSkipped);
    }

    private static int CountNonNullLines(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        return s.Split('\n').Length;
    }

    public static List<string> ParseIps(string ipsRaw, out int duplicatesSkipped)
    {
        duplicatesSkipped = 0;

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var text = (ipsRaw ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        foreach (var line0 in text.Split('\n'))
        {
            var line = (line0 ?? "").Trim();
            if (line.Length == 0) continue;

            // strip /xx
            var slash = line.IndexOf('/');
            if (slash >= 0)
                line = line.Substring(0, slash).Trim();

            if (line.Length == 0) continue;

            if (!seen.Add(line))
            {
                duplicatesSkipped++;
                continue;
            }

            result.Add(line);
        }

        return result;
    }

    public static string EncryptPassword(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";

        var data = Encoding.UTF8.GetBytes(plain);
        var key = SecretKey;

        var xored = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            xored[i] = (byte)(data[i] ^ key[i % key.Length]);

        return Convert.ToBase64String(xored);
    }
}
