using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StruxureGuard.Core.Tools.ModbusGroupAdvisor;

public sealed class ModbusGroupAdvisorEngine
{
    // Same constants as Python
    public const int MaxRegisters = 120;
    public const int MaxCoilBits = 2000;
    public const int MaxRegGap = 9;
    public const int MaxCoilGap = 30;

    public const string XmlVersion = "4.0.3.176";

    public static (List<RegisterEntry> Entries, List<ModbusPreviewRowDto> Preview, List<string> Warnings) ParseInput(string raw)
    {
        raw ??= "";
        var lines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                       .Where(ln => !string.IsNullOrWhiteSpace(ln))
                       .ToList();

        if (lines.Count == 0)
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), new List<string>());

        var warnings = new List<string>();

        var delim = DetectDelimiter(lines);
        var rows = ParseDelimited(lines, delim);

        if (rows.Count == 0)
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), new List<string>());

        var header = rows[0];
        var headerNorm = header.Select(NormHeader).ToList();

        // keys
        string[] NAME_KEYS = ["name", "naam", "pointname", "tag", "objectname"];
        string[] ADDR_KEYS = ["registernumber", "register", "address", "adres", "startaddress",
                              "registeraddress", "regnr", "registernr", "registerno"];
        string[] TYPE_KEYS = ["registertype", "datatype", "type", "pointtype"];
        string[] FC_KEYS   = ["readfunctioncode", "functioncode", "readfunction", "fc", "readfc", "modbusfunction", "function"];

        var nameI = FindColIndex(headerNorm, NAME_KEYS);
        var addrI = FindColIndex(headerNorm, ADDR_KEYS);
        var typeI = FindColIndex(headerNorm, TYPE_KEYS);
        var fcI   = FindColIndex(headerNorm, FC_KEYS);

        var hasHeader = (nameI is not null && addrI is not null && fcI is not null);

        if (!hasHeader)
        {
            // Matches your Python decision: no fallback rows, just warn and parse nothing
            warnings.Add("Header niet herkend; plak bij voorkeur export met kolomkoppen (Name/Register number/Read function code/...).");
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), warnings);
        }

        var dataRows = rows.Skip(1).ToList();

        var entries = new List<RegisterEntry>();
        var preview = new List<ModbusPreviewRowDto>();
        var errors = new List<string>();

        var rowIndex = 1; // header = 1
        foreach (var r in dataRows)
        {
            rowIndex++;

            if (r.All(cell => string.IsNullOrWhiteSpace(cell)))
                continue;

            try
            {
                var name = SafeCell(r, nameI.Value);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Row{rowIndex}";

                var addrRaw = SafeCell(r, addrI.Value);
                if (string.IsNullOrWhiteSpace(addrRaw))
                    throw new InvalidOperationException("Geen register/adres gevonden");

                var address = ParseAddress(addrRaw);

                var fcRaw = SafeCell(r, fcI.Value);
                if (string.IsNullOrWhiteSpace(fcRaw))
                    throw new InvalidOperationException("Geen functiecode gevonden");

                var functionCode = ParseFc(fcRaw);

                var regTypeRaw = (typeI is null) ? "" : SafeCell(r, typeI.Value);
                var length = LengthFromType(regTypeRaw);
                var regType = RTypeFromTypeAndFc(regTypeRaw, functionCode);

                entries.Add(new RegisterEntry
                {
                    Name = name,
                    Address = address,
                    Length = length,
                    FunctionCode = functionCode,
                    RegType = regType
                });

                preview.Add(new ModbusPreviewRowDto
                {
                    Name = name,
                    Address = address,
                    Length = length,
                    FunctionCode = functionCode,
                    RegType = regType,
                    RawType = regTypeRaw ?? ""
                });
            }
            catch (Exception ex)
            {
                errors.Add($"Regel {rowIndex}: {ex.Message}");
            }
        }

        // same spirit as python: if no entries and we have errors -> hard fail
        if (errors.Count > 0 && entries.Count == 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(20)));

        // if partial success -> warnings
        if (errors.Count > 0 && entries.Count > 0)
        {
            warnings.Add("Er waren waarschuwingen / overgeslagen regels:");
            warnings.AddRange(errors.Take(12));
            if (errors.Count > 12) warnings.Add("...");
        }

        return (entries, preview, warnings);
    }

    public static List<RegisterGroup> BuildGroups(List<RegisterEntry> entries)
    {
        var grouped = entries
            .GroupBy(e => (e.FunctionCode, e.RegType))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Address).ToList());

        var result = new List<RegisterGroup>();
        var groupCounter = 1;

        foreach (var kv in grouped)
        {
            var fc = kv.Key.FunctionCode;
            var rtype = kv.Key.RegType;
            var lst = kv.Value;

            var maxUnits = rtype == "register" ? MaxRegisters : MaxCoilBits;
            var maxGap = rtype == "register" ? MaxRegGap : MaxCoilGap;

            var currentEntries = new List<RegisterEntry>();
            var currentHasGaps = false;
            int currentUnits = 0;
            int currentStart = 0;
            int currentEnd = 0;
            int prevEnd = 0;

            void Flush()
            {
                if (currentEntries.Count == 0) return;

                result.Add(new RegisterGroup
                {
                    GroupId = groupCounter,
                    FunctionCode = fc,
                    RegType = rtype,
                    StartAddress = currentStart,
                    EndAddress = currentEnd,
                    TotalUnits = currentUnits,
                    NumPoints = currentEntries.Count,
                    HasGaps = currentHasGaps,
                    Entries = currentEntries.ToList()
                });

                groupCounter++;
                currentEntries.Clear();
                currentHasGaps = false;
                currentUnits = 0;
                currentStart = 0;
                currentEnd = 0;
                prevEnd = 0;
            }

            foreach (var e in lst)
            {
                var eStart = e.Address;
                var eEnd = e.Address + e.Length - 1;
                var eUnits = e.Length;

                if (currentEntries.Count == 0)
                {
                    currentEntries.Add(e);
                    currentUnits = eUnits;
                    currentStart = eStart;
                    currentEnd = eEnd;
                    prevEnd = eEnd;
                    continue;
                }

                var gap = eStart - prevEnd - 1;
                var wouldUnits = currentUnits + eUnits;

                if (gap > maxGap || wouldUnits > maxUnits)
                {
                    Flush();
                    currentEntries.Add(e);
                    currentUnits = eUnits;
                    currentStart = eStart;
                    currentEnd = eEnd;
                    prevEnd = eEnd;
                }
                else
                {
                    currentEntries.Add(e);
                    currentUnits = wouldUnits;
                    currentEnd = Math.Max(currentEnd, eEnd);
                    if (gap > 0) currentHasGaps = true;
                    prevEnd = eEnd;
                }
            }

            Flush();
        }

        result.Sort((a, b) =>
        {
            var c = a.FunctionCode.CompareTo(b.FunctionCode);
            if (c != 0) return c;
            c = string.Compare(a.RegType, b.RegType, StringComparison.Ordinal);
            if (c != 0) return c;
            return a.StartAddress.CompareTo(b.StartAddress);
        });

        return result;
    }

    public static string BuildEboXml(List<RegisterGroup> groups)
    {
        var objectSet = new XElement("ObjectSet",
            new XAttribute("ExportMode", "Special"),
            new XAttribute("Note", "TypesFirst"),
            new XAttribute("SemanticsFilter", "Special"),
            new XAttribute("Version", XmlVersion)
        );

        var meta = new XElement("MetaInformation",
            new XElement("ExportMode", new XAttribute("Value", "Special")),
            new XElement("SemanticsFilter", new XAttribute("Value", "None")),
            new XElement("RuntimeVersion", new XAttribute("Value", XmlVersion)),
            new XElement("SourceVersion", new XAttribute("Value", XmlVersion)),
            new XElement("ServerFullPath", new XAttribute("Value", ""))
        );
        objectSet.Add(meta);

        var exported = new XElement("ExportedObjects");
        objectSet.Add(exported);

        foreach (var g in groups)
        {
            var name = $"Modbus Register Group FC{g.FunctionCode} {g.StartAddress} - {g.EndAddress}";
            var oi = new XElement("OI",
                new XAttribute("NAME", name),
                new XAttribute("TYPE", "modbus.point.ModbusRegisterGroup")
            );

            var piCt = new XElement("PI", new XAttribute("Name", "ContentType"),
                new XElement("Reference",
                    new XAttribute("DeltaFilter", "0"),
                    new XAttribute("Locked", "1"),
                    new XAttribute("Object", "~/System/Content Types/mapModbus"),
                    new XAttribute("Retransmit", "0"),
                    new XAttribute("TransferRate", "10")
                )
            );

            oi.Add(piCt);
            oi.Add(new XElement("PI", new XAttribute("Name", "GroupReadCode"), new XAttribute("Value", g.FunctionCode.ToString(CultureInfo.InvariantCulture))));
            oi.Add(new XElement("PI", new XAttribute("Name", "UseContentTypeFromRule"), new XAttribute("Value", "0")));

            exported.Add(oi);
        }

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), objectSet);

        // pretty-print
        return doc.ToString(SaveOptions.None);
    }

    // ---------------- helpers ----------------

    private static string SafeCell(List<string> row, int idx)
        => (idx >= 0 && idx < row.Count) ? (row[idx] ?? "").Trim() : "";

    private static string NormHeader(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        foreach (var ch in new[] { " ", "_", "-", ".", ":", "/", "\\", "(", ")", "[", "]" })
            s = s.Replace(ch, "");
        return s;
    }

    private static char DetectDelimiter(List<string> lines)
    {
        foreach (var ln in lines)
        {
            if (string.IsNullOrWhiteSpace(ln)) continue;
            if (ln.Contains('\t')) return '\t';
            if (ln.Contains(';')) return ';';
            if (ln.Contains(',')) return ',';
            return '\t';
        }
        return '\t';
    }

    private static List<List<string>> ParseDelimited(List<string> lines, char delim)
    {
        // Simple CSV-ish split (sufficient for typical EBO/Excel exports)
        // If you need quoted CSV later, we can upgrade safely.
        var result = new List<List<string>>();
        foreach (var ln in lines)
        {
            var parts = ln.Split(delim);
            result.Add(parts.Select(p => p?.Trim() ?? "").ToList());
        }
        return result;
    }

    private static int? FindColIndex(List<string> headersNorm, IEnumerable<string> candidates)
    {
        var candNorm = candidates.Select(NormHeader).Where(x => x.Length > 0).ToList();

        for (int i = 0; i < headersNorm.Count; i++)
            if (candNorm.Contains(headersNorm[i]))
                return i;

        for (int i = 0; i < headersNorm.Count; i++)
            foreach (var c in candNorm)
                if (c.Length > 0 && headersNorm[i].Contains(c, StringComparison.Ordinal))
                    return i;

        return null;
    }

    private static int ParseFc(string s)
    {
        var t = (s ?? "").Trim().ToLowerInvariant();
        t = t.Replace("fc", "").Replace("function", "").Replace("code", "").Trim();
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            throw new InvalidOperationException($"Geen functiecode in '{s}'");
        return int.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static int ParseAddress(string s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) throw new InvalidOperationException("Leeg adres");

        t = t.Replace(" ", "");

        // 1.234.567 or 1,234,567
        if (Regex.IsMatch(t, @"^\d{1,3}([.,]\d{3})+$"))
        {
            t = t.Replace(".", "").Replace(",", "");
            return int.Parse(t, CultureInfo.InvariantCulture);
        }

        // float-ish to int
        if (double.TryParse(t.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
        {
            if (Math.Abs(f - Math.Round(f)) < 1e-9)
                return (int)Math.Round(f);
        }

        return int.Parse(t, CultureInfo.InvariantCulture);
    }

    private static int LengthFromType(string regtype)
    {
        var t = (regtype ?? "").ToLowerInvariant();
        if (t.Contains("64")) return 4;
        if (t.Contains("32")) return 2;
        if (t.Contains("16")) return 1;
        if (t.Contains("coil") || t.Contains("discrete") || t.Contains("bool")) return 1;
        return 1;
    }

    private static string RTypeFromTypeAndFc(string regtype, int fc)
    {
        var t = (regtype ?? "").ToLowerInvariant();
        if (t.Contains("16 bit") || t.Contains("32 bit") || t.Contains("64 bit"))
            return "register";
        if (t.Contains("coil") || t.Contains("discrete") || t.Contains("bool"))
            return "coil";
        if (fc is 1 or 2 or 15)
            return "coil";
        return "register";
    }
}
