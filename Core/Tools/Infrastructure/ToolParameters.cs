using System.Collections;
using System.Globalization;
using System.Text;

namespace StruxureGuard.Core.Tools.Infrastructure;

/// <summary>
/// Immutable-ish parameter bag for tool runs. Wraps a string->string dictionary
/// and provides typed access helpers + safe log formatting.
/// </summary>
public sealed class ToolParameters : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _data;

    public static ToolParameters Empty { get; } =
        new ToolParameters(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private ToolParameters(Dictionary<string, string> data)
    {
        _data = data;
    }

    public static ToolParameters From(IReadOnlyDictionary<string, string> src)
    {
        if (src.Count == 0) return Empty;

        var d = new Dictionary<string, string>(src.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in src)
            d[kv.Key] = kv.Value;

        return new ToolParameters(d);
    }

    public ToolParameters With(string key, string value)
    {
        var d = new Dictionary<string, string>(_data, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        };
        return new ToolParameters(d);
    }

    // ---- Typed access helpers ----

    public string? GetString(string key)
        => TryGetValue(key, out var v) ? v : null;

    public string GetRequiredString(string key)
    {
        if (!TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            throw new KeyNotFoundException($"Required parameter '{key}' is missing/empty.");
        return v;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!TryGetValue(key, out var v)) return defaultValue;
        if (bool.TryParse(v, out var b)) return b;

        // Accept 0/1
        if (v == "0") return false;
        if (v == "1") return true;

        return defaultValue;
    }

    public int? GetInt32(string key)
    {
        if (!TryGetValue(key, out var v)) return null;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        return null;
    }

    /// <summary>
    /// Safe-ish, trimmed parameter formatting for logs.
    /// - Sorts keys
    /// - Limits number of pairs and value length
    /// - Replaces CR/LF/TAB
    /// </summary>
    public string ToLogString(int maxPairs = 25, int maxValueLen = 140)
    {
        if (_data.Count == 0) return "";

        var keys = _data.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var take = Math.Min(maxPairs, keys.Count);
        var sb = new StringBuilder(capacity: take * 24);

        for (int i = 0; i < take; i++)
        {
            var k = keys[i];
            var v = _data.TryGetValue(k, out var val) ? val : "";

            v ??= "";
            v = v.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

            if (v.Length > maxValueLen)
                v = v.Substring(0, maxValueLen) + "…";

            if (sb.Length > 0) sb.Append("; ");
            sb.Append(k).Append("='").Append(v).Append('\'');
        }

        if (keys.Count > take)
        {
            sb.Append("; ...(+").Append(keys.Count - take).Append(')');
        }

        return sb.ToString();
    }

    // ---- IReadOnlyDictionary implementation ----

    public int Count => _data.Count;

    public IEnumerable<string> Keys => _data.Keys;

    public IEnumerable<string> Values => _data.Values;

    public string this[string key] => _data[key];

    public bool ContainsKey(string key) => _data.ContainsKey(key);

    public bool TryGetValue(string key, out string value) => _data.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
}
