using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StruxureGuard.Core.Tools.NotificationPullerConfig;

public sealed record NotificationPullerConfigFile
{
    [JsonPropertyName("ips")]
    public required List<string> Ips { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password_enc")]
    public required string PasswordEnc { get; init; }

    [JsonPropertyName("password_encrypted")]
    public bool PasswordEncrypted { get; init; } = true;

    [JsonPropertyName("secondary_password_enc")]
    public required string SecondaryPasswordEnc { get; init; }

    [JsonPropertyName("secondary_password_encrypted")]
    public bool SecondaryPasswordEncrypted { get; init; } = true;

    [JsonPropertyName("tertiary_password_enc")]
    public required string TertiaryPasswordEnc { get; init; }

    [JsonPropertyName("tertiary_password_encrypted")]
    public bool TertiaryPasswordEncrypted { get; init; } = true;

    [JsonPropertyName("ssh_port")]
    public int SshPort { get; init; }

    [JsonPropertyName("remote_notifications_dir")]
    public required string RemoteNotificationsDir { get; init; }

    [JsonPropertyName("remote_backup_dir")]
    public string RemoteBackupDir { get; init; } = "/var/sbo/db_backup/LocalBackup";

    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("export_root")]
    public required string ExportRoot { get; init; }

    [JsonPropertyName("make_zip")]
    public bool MakeZip { get; init; }

    [JsonPropertyName("save_mode")]
    public required string SaveMode { get; init; } // all/latest

    [JsonPropertyName("delete_oh")]
    public bool DeleteOh { get; init; }

    [JsonPropertyName("delete_assets")]
    public bool DeleteAssets { get; init; }

    [JsonPropertyName("delete_custom")]
    public bool DeleteCustom { get; init; }

    [JsonPropertyName("delete_custom_pattern")]
    public required string DeleteCustomPattern { get; init; }

    [JsonPropertyName("delete_all")]
    public bool DeleteAll { get; init; }

    [JsonPropertyName("timeout")]
    public int Timeout { get; init; } = 20;

    [JsonPropertyName("scheme")]
    public string Scheme { get; init; } = "ssh";
}

public sealed record NotificationPullerConfigBuildResult(
    NotificationPullerConfigFile Config,
    string Json,
    int IpsInputLines,
    int IpsParsedCount,
    int IpsDuplicatesSkipped);
