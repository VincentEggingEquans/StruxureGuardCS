using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using StruxureGuard.Core.Tools;

namespace StruxureGuard.UI.Tools;

public static class ToolCatalog
{
    private sealed record ToolEntry(ToolDefinition Definition, Func<Form> Factory);

    // Single source of truth: key -> (metadata + factory)
    private static readonly IReadOnlyDictionary<string, ToolEntry> _tools =
        new Dictionary<string, ToolEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [ToolKeys.Mkdir] = new(
                new ToolDefinition(ToolKeys.Mkdir, "MKDIR"),
                () => new MkdirToolForm()),

            [ToolKeys.ExcelWriter] = new(
                new ToolDefinition(ToolKeys.ExcelWriter, "ExcelWriter"),
                () => new ExcelWriterToolForm()),

            [ToolKeys.UrlDecoder] = new(
                new ToolDefinition(ToolKeys.UrlDecoder, "URL Decoder"),
                () => new UrlDecoderToolForm()),

            [ToolKeys.LineConverter] = new(
                new ToolDefinition(ToolKeys.LineConverter, "Lineconverter"),
                () => new LineConverterToolForm()),

            [ToolKeys.ExcelRenamer] = new(
                new ToolDefinition(ToolKeys.ExcelRenamer, "ExcelRenamer"),
                () => new ExcelRenamerToolForm()),

            [ToolKeys.AspPathChecker] = new(
                new ToolDefinition(ToolKeys.AspPathChecker, "ASP Path Checker"),
                () => new AspPathCheckerToolForm()),

            [ToolKeys.NotificationPullerConfig] = new(
                new ToolDefinition(ToolKeys.NotificationPullerConfig, "NotificationPuller Config"),
                () => new NotificationPullerConfigToolForm()),

            [ToolKeys.ModbusGroupAdvisor] = new(
                new ToolDefinition(ToolKeys.ModbusGroupAdvisor, "Modbus Group Advisor"),
                () => new ModbusGroupAdvisorToolForm()),
        };

    // Pure metadata (Core-type)
    public static IReadOnlyList<ToolDefinition> Tools { get; } =
        _tools.Values.Select(v => v.Definition).ToList();

    // UI-only: mapping key -> Form
    public static Form CreateForm(string key)
    {
        if (_tools.TryGetValue(key, out var entry))
            return entry.Factory();

        throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown tool key");
    }

    public static bool IsKnownToolKey(string key)
        => _tools.ContainsKey(key);

    public static string? GetButtonText(string key)
        => _tools.TryGetValue(key, out var entry) ? entry.Definition.ButtonText : null;
}
