using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PowerManagerMod.Core;

/// <summary>
/// One device across game sessions: its furniture type and where it stands, to a tenth of a metre. A device taken down
/// and built again in the same spot is the same device to the player, so it keeps its priority.
/// </summary>
public readonly record struct DeviceKey(int ConfigId, long X, long Y, long Z)
{
    private const char Separator = ':';

    public static DeviceKey Of(int configId, double x, double y, double z) => new(configId, Tenths(x), Tenths(y), Tenths(z));

    public static bool TryParse(string text, out DeviceKey key)
    {
        var fields = text.Split(Separator);
        key = default;

        if (fields.Length != 4 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var configId)) return false;
        if (!TryLong(fields[1], out var x) || !TryLong(fields[2], out var y) || !TryLong(fields[3], out var z)) return false;

        key = new DeviceKey(configId, x, y, z);

        return true;
    }

    public override string ToString() => string.Join(Separator, ConfigId.ToString(CultureInfo.InvariantCulture), Text(X), Text(Y), Text(Z));

    private static long Tenths(double metres) => (long)Math.Round(metres * 10, MidpointRounding.AwayFromZero);

    private static string Text(long tenths) => tenths.ToString(CultureInfo.InvariantCulture);

    private static bool TryLong(string text, out long value) => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}

/// <summary>
/// What the mod keeps per save: the devices marked low priority, and the ones it switched off to save the battery (so
/// it switches back on only those, never one the player turned off).
/// </summary>
public sealed record DeviceMarks(IReadOnlyCollection<DeviceKey> LowPriority, IReadOnlyCollection<DeviceKey> Shed)
{
    public static readonly DeviceMarks None = new(Array.Empty<DeviceKey>(), Array.Empty<DeviceKey>());
}

/// <summary>
/// The devices file: one tab-separated line per mark, "save, low or shed, device key", for every save at once. The save
/// is URI-escaped so a tab or a line break in its name cannot split a line. Writing one save's marks replaces only the
/// lines the mod reads for it, so every other line stays exactly as it was.
/// </summary>
public static class DeviceFile
{
    public const string Header = "# Power Manager devices. One line per mark: save, low or shed, device key.";

    private const char Separator = '\t';
    private const string LowKind = "low";
    private const string ShedKind = "shed";

    public static DeviceMarks Read(string? text, string save)
    {
        var low = new HashSet<DeviceKey>();
        var shed = new HashSet<DeviceKey>();

        foreach (var fields in Lines(text).Select(Fields))
        {
            if (!IsMark(fields, save, out var key)) continue;

            (fields[1] == LowKind ? low : shed).Add(key);
        }

        return new DeviceMarks(low, shed);
    }

    public static string Write(string? text, string save, DeviceMarks marks)
    {
        var file = new StringBuilder().Append(Header).Append('\n');
        var escaped = Uri.EscapeDataString(save);

        foreach (var line in Lines(text).Where(line => !IsMark(Fields(line), save, out _))) file.Append(line).Append('\n');

        Append(file, escaped, LowKind, marks.LowPriority);
        Append(file, escaped, ShedKind, marks.Shed);

        return file.ToString();
    }

    private static void Append(StringBuilder file, string save, string kind, IEnumerable<DeviceKey> keys)
    {
        foreach (var key in keys.Select(key => key.ToString()).OrderBy(key => key, StringComparer.Ordinal)) file.Append(string.Join(Separator, save, kind, key)).Append('\n');
    }

    private static IEnumerable<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0 && line != Header);

    private static string[] Fields(string line) => line.StartsWith('#') ? Array.Empty<string>() : line.Split(Separator);

    private static bool IsMark(string[] fields, string save, out DeviceKey key)
    {
        key = default;

        return fields.Length == 3 && (fields[1] == LowKind || fields[1] == ShedKind) && Unescape(fields[0]) == save && DeviceKey.TryParse(fields[2], out key);
    }

    private static string? Unescape(string text)
    {
        try
        {
            return Uri.UnescapeDataString(text);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
