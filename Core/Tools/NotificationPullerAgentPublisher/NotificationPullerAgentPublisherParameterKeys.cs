namespace StruxureGuard.Core.Tools.NotificationPullerAgentPublisher;

public static class NotificationPullerAgentPublisherParameterKeys
{
    public const string OutputFolder = "output_folder";
    public const string ConfigPath = "config_path";
    public const string CopyConfig = "copy_config";

    public const string Runtime = "runtime";                 // default: win-x64
    public const string Configuration = "configuration";     // default: Release
    public const string SelfContained = "self_contained";    // default: true
    public const string SingleFile = "single_file";          // default: true
}
