using System.Text.Json;

namespace Shared.Cdc;

public static class CdcMetadata
{
    public static long ReadLsn(JsonElement root) =>
        root.TryGetProperty("__lsn", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0L;

    public static bool ReadIsDeleted(JsonElement root)
    {
        if (!root.TryGetProperty("__deleted", out var e)) return false;
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => e.GetString() == "true",
            _ => false,
        };
    }

    public static int Int(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var e)) return 0;
        return e.ValueKind == JsonValueKind.Number ? e.GetInt32() : int.Parse(e.GetString() ?? "0");
    }

    public static string String(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var e) || e.ValueKind == JsonValueKind.Null) return string.Empty;
        return e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString();
    }

    public static bool Bool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var e)) return false;
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => e.GetString() == "true",
            _ => false,
        };
    }

    public static decimal Decimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var e) || e.ValueKind == JsonValueKind.Null) return 0m;
        return e.ValueKind == JsonValueKind.String
            ? decimal.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : e.GetDecimal();
    }
}
