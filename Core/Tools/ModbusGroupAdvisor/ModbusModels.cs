namespace StruxureGuard.Core.Tools.ModbusGroupAdvisor;

public sealed class RegisterEntry
{
    public required string Name { get; init; }
    public int Address { get; init; }
    public int Length { get; init; }
    public int FunctionCode { get; init; }
    public required string RegType { get; init; } // "register" or "coil"
}

public sealed class RegisterGroup
{
    public int GroupId { get; init; }
    public int FunctionCode { get; init; }
    public required string RegType { get; init; }

    public int StartAddress { get; init; }
    public int EndAddress { get; init; }

    public int TotalUnits { get; init; }     // regs or bits
    public int NumPoints { get; init; }
    public bool HasGaps { get; init; }

    public required List<RegisterEntry> Entries { get; init; }
}

// DTOs for UI transport (ToolResult.Outputs as JSON)
public sealed class ModbusPreviewRowDto
{
    public required string Name { get; init; }
    public int Address { get; init; }
    public int Length { get; init; }
    public int FunctionCode { get; init; }
    public required string RegType { get; init; }
    public required string RawType { get; init; }
}

public sealed class ModbusRejectedRowDto
{
    public int RowNumber { get; init; }          // 1-based line number (header=1)
    public string? Name { get; init; }           // best-effort, may be null
    public required string Reason { get; init; } // why parse failed
    public required string RawLine { get; init; }// original raw line
}

public sealed class ModbusEntryDto
{
    public required string Name { get; init; }
    public int Address { get; init; }
    public int Length { get; init; }
    public int FunctionCode { get; init; }
    public required string RegType { get; init; }
}

public sealed class ModbusGroupDto
{
    public int GroupId { get; init; }
    public int FunctionCode { get; init; }
    public required string RegType { get; init; }
    public int StartAddress { get; init; }
    public int EndAddress { get; init; }
    public int TotalUnits { get; init; }
    public int NumPoints { get; init; }
    public bool HasGaps { get; init; }
    public required List<ModbusEntryDto> Entries { get; init; }
}

public sealed class ModbusAnalysisResultDto
{
    public required List<ModbusPreviewRowDto> PreviewRows { get; init; }

    // NEW: rows that could not be parsed (for red preview highlighting etc.)
    public required List<ModbusRejectedRowDto> RejectedRows { get; init; }

    public required List<ModbusGroupDto> Groups { get; init; }
    public required List<string> Warnings { get; init; }
}
