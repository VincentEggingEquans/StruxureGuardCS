using StruxureGuard.Core.Tools;

namespace StruxureGuard.UI.Tools;

public static class ToolCatalog
{
    // Pure metadata (Core-type)
    public static IReadOnlyList<ToolDefinition> Tools { get; } = new List<ToolDefinition>
{
    new(ToolKeys.Mkdir, "MKDIR"),
    new(ToolKeys.ExcelWriter, "ExcelWriter"),
    new(ToolKeys.UrlDecoder, "URL Decoder"),
    new(ToolKeys.LineConverter, "Lineconverter"),
    new(ToolKeys.ExcelRenamer, "ExcelRenamer"),
    new(ToolKeys.AspPathChecker, "ASP Path Checker"),
    new(ToolKeys.NotificationPullerConfig, "NotificationPuller Config"),
    new(ToolKeys.ModbusGroupAdvisor, "Modbus Group Advisor"),
};


    // UI-only: mapping key -> Form
public static Form CreateForm(string key) => key.ToLowerInvariant() switch
{
    var k when k == ToolKeys.Mkdir => new MkdirToolForm(),
    var k when k == ToolKeys.ExcelWriter => new ExcelWriterToolForm(),
    var k when k == ToolKeys.UrlDecoder => new UrlDecoderToolForm(),
    var k when k == ToolKeys.LineConverter => new LineConverterToolForm(),
    var k when k == ToolKeys.ExcelRenamer => new ExcelRenamerToolForm(),
    var k when k == ToolKeys.AspPathChecker => new AspPathCheckerToolForm(),
    var k when k == ToolKeys.NotificationPullerConfig => new NotificationPullerConfigToolForm(),
    var k when k == ToolKeys.ModbusGroupAdvisor => new ModbusGroupAdvisorToolForm(),
    _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown tool key")
};

public static bool IsKnownToolKey(string key)
    => Tools.Any(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

public static string? GetButtonText(string key)
    => Tools.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))?.ButtonText;



}
