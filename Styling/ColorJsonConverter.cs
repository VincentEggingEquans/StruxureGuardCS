using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StruxureGuard.Styling;

public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? "#000000";
        if (!s.StartsWith("#")) return Color.Black;

        if (s.Length == 7)
            return ColorTranslator.FromHtml(s);

        if (s.Length == 9)
        {
            var a = Convert.ToInt32(s.Substring(1, 2), 16);
            var r = Convert.ToInt32(s.Substring(3, 2), 16);
            var g = Convert.ToInt32(s.Substring(5, 2), 16);
            var b = Convert.ToInt32(s.Substring(7, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }

        return Color.Black;
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        => writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
}

public sealed class NullableColorJsonConverter : JsonConverter<Color?>
{
    private static readonly ColorJsonConverter Inner = new();

    public override Color? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var c = Inner.Read(ref reader, typeof(Color), options);
        return c;
    }

    public override void Write(Utf8JsonWriter writer, Color? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        Inner.Write(writer, value.Value, options);
    }
}
