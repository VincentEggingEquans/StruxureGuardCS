// File: Agent/NotificationPuller/Config/NotificationPullerAgentConfigLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StruxureGuard.NotificationPullerAgent.Config
{
    public static class NotificationPullerAgentConfigLoader
    {
        public static NotificationPullerAgentConfig Load(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("configPath is empty.");

            if (!File.Exists(configPath))
                throw new FileNotFoundException("Config file not found.", configPath);

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cfg = new NotificationPullerAgentConfig
            {
                ExportRoot = GetString(root, "ExportRoot") ?? "",
                RemoteNotificationsDir = GetString(root, "RemoteNotificationsDir") ?? "/var/sbo/db/notifications",
                Pattern = GetString(root, "Pattern") ?? "*",
                SaveMode = GetString(root, "SaveMode") ?? "all",
                IpsRaw = GetString(root, "IpsRaw") ?? "",
                SshPort = GetInt(root, "SshPort") ?? 22,
                Username = GetString(root, "Username") ?? "",
                Password1 = GetString(root, "Password1") ?? "",
                Password2 = GetString(root, "Password2") ?? "",
                Password3 = GetString(root, "Password3") ?? "",
                MakeZip = GetBool(root, "MakeZip") ?? true
            };

            cfg.Ips = NormalizeIps(cfg.IpsRaw, cfg.Ips);

            if (string.IsNullOrWhiteSpace(cfg.ExportRoot))
                cfg.ExportRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NotificationPuller_Export");

            if (cfg.SshPort <= 0) cfg.SshPort = 22;

            return cfg;
        }

        private static List<string> NormalizeIps(string ipsRaw, List<string> ips)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ips != null)
            {
                foreach (var ip in ips)
                {
                    var n = NormalizeIpToken(ip);
                    if (!string.IsNullOrWhiteSpace(n)) set.Add(n);
                }
            }

            if (!string.IsNullOrWhiteSpace(ipsRaw))
            {
                var lines = ipsRaw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var n = NormalizeIpToken(line);
                    if (!string.IsNullOrWhiteSpace(n)) set.Add(n);
                }
            }

            return new List<string>(set);
        }

        private static string NormalizeIpToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "";
            var t = token.Trim();
            var slash = t.IndexOf('/');
            if (slash >= 0) t = t.Substring(0, slash);
            t = t.Trim().Trim(',').Trim(';');
            return t;
        }

        private static string? GetString(JsonElement root, string prop)
        {
            foreach (var p in root.EnumerateObject())
                if (string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase))
                    return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
            return null;
        }

        private static int? GetInt(JsonElement root, string prop)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (!string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)) continue;
                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var v)) return v;
                if (p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Value.GetString(), out var s)) return s;
            }
            return null;
        }

        private static bool? GetBool(JsonElement root, string prop)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (!string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)) continue;
                if (p.Value.ValueKind == JsonValueKind.True) return true;
                if (p.Value.ValueKind == JsonValueKind.False) return false;
                if (p.Value.ValueKind == JsonValueKind.String && bool.TryParse(p.Value.GetString(), out var b)) return b;
            }
            return null;
        }
    }
}
