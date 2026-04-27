using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Tolerant enum converter for LLM JSON output: accepts numeric values, mixed case,
/// and compound strings like "Medium-Hard" (picks the highest-ranked match).
/// </summary>
public sealed class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly char[] Separators = { '-', '/', ',', ' ', '_', '|' };

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(T), n))
            return (T)Enum.ToObject(typeof(T), n);

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return default;

        if (Enum.TryParse<T>(raw, ignoreCase: true, out var direct)) return direct;

        T? highest = null;
        foreach (var part in raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<T>(part.Trim(), ignoreCase: true, out var match)) continue;
            if (highest is null || Convert.ToInt32(match) > Convert.ToInt32(highest.Value))
                highest = match;
        }
        if (highest.HasValue) return highest.Value;

        throw new JsonException($"Cannot convert '{raw}' to {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

/// <summary>Applies <see cref="LenientEnumConverter{T}"/> to every enum type seen by the serializer.</summary>
public sealed class LenientEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(LenientEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
