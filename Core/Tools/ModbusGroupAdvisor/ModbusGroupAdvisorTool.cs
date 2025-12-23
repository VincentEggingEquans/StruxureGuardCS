using System.Text.Json;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.ModbusGroupAdvisor;

public sealed class ModbusGroupAdvisorTool : ITool
{
    public string ToolKey => ToolKeys.ModbusGroupAdvisor;

    public const string OutputKeyAnalysisJson = "AnalysisJson";
    public const string OutputKeyEboXml = "EboXml";

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        var raw = ctx.Parameters.GetString(ModbusGroupAdvisorParameterKeys.RawText) ?? "";
        if (string.IsNullOrWhiteSpace(raw))
            r.AddError("modbus.input", "Plak eerst een lijst met registers.");

        return r;
    }

    public Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var raw = ctx.Parameters.GetRequiredString(ModbusGroupAdvisorParameterKeys.RawText);

        Log.Info("modbus", $"Analyse start runId='{ctx.RunId}' rawLen={raw.Length}");

        progress?.Report(new ToolProgressInfo(0, 100, null, "Parsing input...", "Parse", 5));

        var (entries, preview, rejected, warnings) = ModbusGroupAdvisorEngine.ParseInputDetailed(raw);

        ct.ThrowIfCancellationRequested();

        progress?.Report(new ToolProgressInfo(0, 100, null, $"Parsed entries={entries.Count} rejected={rejected.Count}", "Parse", 30));

        var groups = (entries.Count == 0)
            ? new List<RegisterGroup>()
            : ModbusGroupAdvisorEngine.BuildGroups(entries);

        progress?.Report(new ToolProgressInfo(0, 100, null, $"Groups={groups.Count}", "Group", 70));

        var dto = new ModbusAnalysisResultDto
        {
            PreviewRows = preview,
            RejectedRows = rejected,
            Groups = groups.Select(g => new ModbusGroupDto
            {
                GroupId = g.GroupId,
                FunctionCode = g.FunctionCode,
                RegType = g.RegType,
                StartAddress = g.StartAddress,
                EndAddress = g.EndAddress,
                TotalUnits = g.TotalUnits,
                NumPoints = g.NumPoints,
                HasGaps = g.HasGaps,
                Entries = g.Entries
                    .OrderBy(e => e.Address)
                    .Select(e => new ModbusEntryDto
                    {
                        Name = e.Name,
                        Address = e.Address,
                        Length = e.Length,
                        FunctionCode = e.FunctionCode,
                        RegType = e.RegType
                    }).ToList()
            }).ToList(),
            Warnings = warnings
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false });

        string xml = "";
        if (groups.Count > 0)
            xml = ModbusGroupAdvisorEngine.BuildEboXml(groups);

        progress?.Report(new ToolProgressInfo(1, 1, null, "Done", "Done", 100));

        var res = ToolResult.Ok(summary: $"Parsed={entries.Count} Groups={groups.Count} Rejected={rejected.Count}")
            .WithOutput(OutputKeyAnalysisJson, json)
            .WithOutput(OutputKeyEboXml, xml);

        foreach (var w in warnings)
            res.WithWarning(w);

        if (entries.Count == 0 && warnings.Count == 0)
            res.WithWarning("Geen geldige punten gevonden.");

        Log.Info("modbus",
            $"Analyse done runId='{ctx.RunId}' entries={entries.Count} groups={groups.Count} rejected={rejected.Count} warnings={warnings.Count}");

        return Task.FromResult(res);
    }
}
