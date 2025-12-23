using System.Globalization;
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

    // Backwards compatible signature (existing callers)
    public static (List<RegisterEntry> Entries, List<ModbusPreviewRowDto> Preview, List<string> Warnings) ParseInput(string raw)
    {
        var (entries, preview, _, warnings) = ParseInputDetailed(raw);
        return (entries, preview, warnings);
    }

    // NEW: also returns rejected rows
    public static (List<RegisterEntry> Entries,
                   List<ModbusPreviewRowDto> Preview,
                   List<ModbusRejectedRowDto> Rejected,
                   List<string> Warnings) ParseInputDetailed(string raw)
    {
        raw ??= "";
        var lines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                       .Where(ln => !string.IsNullOrWhiteSpace(ln))
                       .ToList();

        if (lines.Count == 0)
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), new List<ModbusRejectedRowDto>(), new List<string>());

        var warnings = new List<string>();

        var delim = DetectDelimiter(lines);
        var rows = ParseDelimited(lines, delim);

        if (rows.Count == 0)
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), new List<ModbusRejectedRowDto>(), new List<string>());

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

        // Require at least name+addr+fc to produce useful groups
        if (nameI is null || addrI is null || fcI is null)
        {
            warnings.Add("Header niet herkend; plak bij voorkeur export met kolomkoppen (Name/Register number/Read function code/...).");
            return (new List<RegisterEntry>(), new List<ModbusPreviewRowDto>(), new List<ModbusRejectedRowDto>(), warnings);
        }

        var nameCol = nameI.Value;
        var addrCol = addrI.Value;
        var fcCol = fcI.Value;
        var typeCol = typeI; // optional

        var entries = new List<RegisterEntry>();
        var preview = new List<ModbusPreviewRowDto>();
        var rejected = new List<ModbusRejectedRowDto>();
        var errors = new List<string>();

        var dataRows = rows.Skip(1).ToList();

        // header line is 1, first data line is 2
        var rowIndex = 1;

        foreach (var r in dataRows)
        {
            rowIndex++;

            if (r.All(cell => string.IsNullOrWhiteSpace(cell)))
                continue;

            try
            {
                var name = SafeCell(r, nameCol);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Row{rowIndex}";

                var addrRaw = SafeCell(r, addrCol);
                if (string.IsNullOrWhiteSpace(addrRaw))
                    throw new InvalidOperationException("Geen register/adres gevonden");

                var address = ParseAddress(addrRaw);

                var fcRaw = SafeCell(r, fcCol);
                if (string.IsNullOrWhiteSpace(fcRaw))
                    throw new InvalidOperationException("Geen functiecode gevonden");

                var fc = ParseFc(fcRaw);

                var rawType = (typeCol is null) ? "" : SafeCell(r, typeCol.Value);
                if (string.IsNullOrWhiteSpace(rawType))
                    rawType = "unknown";

                var len = LengthFromType(rawType);
                var regType = RTypeFromTypeAndFc(rawType, fc);

                var entry = new RegisterEntry
                {
                    Name = name,
                    Address = address,
                    Length = len,
                    FunctionCode = fc,
                    RegType = regType
                };

                entries.Add(entry);

                preview.Add(new ModbusPreviewRowDto
                {
                    Name = name,
                    Address = address,
                    Length = len,
                    FunctionCode = fc,
                    RegType = regType,
                    RawType = rawType
                });
            }
            catch (Exception ex)
            {
                var rawLine = (rowIndex - 1 >= 0 && rowIndex - 1 < lines.Count)
                    ? lines[rowIndex - 1]
                    : string.Join(delim.ToString(), r);

                string? nameGuess = null;
                try
                {
                    var n = SafeCell(r, nameCol);
                    if (!string.IsNullOrWhiteSpace(n)) nameGuess = n;
                }
                catch { /* ignore */ }

                rejected.Add(new ModbusRejectedRowDto
                {
                    RowNumber = rowIndex,
                    Name = nameGuess,
                    Reason = ex.Message,
                    RawLine = rawLine
                });

                errors.Add($"Regel {rowIndex}: {ex.Message}");
            }
        }

        // If nothing parsed and errors exist -> hard fail (tool-runner will show fail)
        if (errors.Count > 0 && entries.Count == 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(20)));

        // Partial parse -> warnings + keep rejected list
        if (errors.Count > 0 && entries.Count > 0)
        {
            warnings.Add($"Er waren waarschuwingen / overgeslagen regels: rejected={rejected.Count}");
            warnings.AddRange(errors.Take(12));
            if (errors.Count > 12) warnings.Add("...");
        }

        return (entries, preview, rejected, warnings);
    }

    public static List<RegisterGroup> BuildGroups(List<RegisterEntry> entries)
    {
        var grouped = entries
            .GroupBy(e => (e.FunctionCode, e.RegType))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Address).ToList());

        var allGroups = new List<RegisterGroup>();
        var groupId = 1;

        foreach (var kv in grouped)
        {
            var (fc, rtype) = kv.Key;
            var pts = kv.Value;

            var maxUnits = (rtype == "coil") ? MaxCoilBits : MaxRegisters;
            var maxGap = (rtype == "coil") ? MaxCoilGap : MaxRegGap;

            int? curStart = null;
            int curEnd = 0;
            var curEntries = new List<RegisterEntry>();
            var hasGaps = false;

            void Flush()
            {
                if (curStart is null || curEntries.Count == 0) return;

                var totalUnits = curEnd - curStart.Value + 1;

                allGroups.Add(new RegisterGroup
                {
                    GroupId = groupId++,
                    FunctionCode = fc,
                    RegType = rtype,
                    StartAddress = curStart.Value,
                    EndAddress = curEnd,
                    TotalUnits = totalUnits,
                    NumPoints = curEntries.Count,
                    HasGaps = hasGaps,
                    Entries = curEntries.ToList()
                });

                curStart = null;
                curEnd = 0;
                curEntries.Clear();
                hasGaps = false;
            }

            foreach (var p in pts)
            {
                var pStart = p.Address;
                var pEnd = p.Address + Math.Max(1, p.Length) - 1;

                if (curStart is null)
                {
                    curStart = pStart;
                    curEnd = pEnd;
                    curEntries.Add(p);
                    continue;
                }

                var gap = pStart - curEnd - 1;
                var wouldNeedEnd = Math.Max(curEnd, pEnd);
                var wouldTotalUnits = wouldNeedEnd - curStart.Value + 1;

                if (gap > maxGap || wouldTotalUnits > maxUnits)
                {
                    Flush();
                    curStart = pStart;
                    curEnd = pEnd;
                    curEntries.Add(p);
                    continue;
                }

                if (gap > 0) hasGaps = true;

                curEnd = wouldNeedEnd;
                curEntries.Add(p);
            }

            Flush();
        }

        return allGroups
            .OrderBy(g => g.RegType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.FunctionCode)
            .ThenBy(g => g.StartAddress)
            .ToList();
    }

    public static string BuildEboXml(List<RegisterGroup> groups)
    {
        var root = new XElement("ObjectSet",
            new XAttribute("Version", XmlVersion));

        foreach (var g in groups)
            root.Add(BuildGroupObject(g));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return doc.ToString();
    }

    private static XElement BuildGroupObject(RegisterGroup g)
    {
        var obj = new XElement("Object",
            new XAttribute("Type", "ModbusReadGroup"),
            new XAttribute("Name", $"Group_{g.GroupId}_{g.RegType}_FC{g.FunctionCode}_{g.StartAddress}_{g.EndAddress}"));

        obj.Add(new XElement("Property", new XAttribute("Name", "FunctionCode"), new XAttribute("Value", g.FunctionCode)));
        obj.Add(new XElement("Property", new XAttribute("Name", "RegType"), new XAttribute("Value", g.RegType)));
        obj.Add(new XElement("Property", new XAttribute("Name", "StartAddress"), new XAttribute("Value", g.StartAddress)));
        obj.Add(new XElement("Property", new XAttribute("Name", "EndAddress"), new XAttribute("Value", g.EndAddress)));
        obj.Add(new XElement("Property", new XAttribute("Name", "TotalUnits"), new XAttribute("Value", g.TotalUnits)));

        var points = new XElement("Points");
        foreach (var p in g.Entries.OrderBy(e => e.Address))
        {
            points.Add(new XElement("Point",
                new XAttribute("Name", p.Name),
                new XAttribute("Address", p.Address),
                new XAttribute("Length", p.Length),
                new XAttribute("FunctionCode", p.FunctionCode),
                new XAttribute("RegType", p.RegType)));
        }

        obj.Add(points);
        return obj;
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
            if (string.IsNullOrWhiteSpace(ln))
                continue;

            if (ln.Contains('\t'))
                return '\t';

            if (ln.Count(c => c == ';') > ln.Count(c => c == ','))
                return ';';

            if (ln.Contains(','))
                return ',';
        }

        return '\t';
    }

    private static List<List<string>> ParseDelimited(List<string> lines, char delim)
    {
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

        // common: "3110/32"
        var slash = t.IndexOf('/');
        if (slash >= 0)
            t = t.Substring(0, slash);

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

        // fallback: digits only (handles "3.110")
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
            return int.Parse(digits, CultureInfo.InvariantCulture);

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
