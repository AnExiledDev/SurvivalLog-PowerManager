using System;
using System.Globalization;

namespace PowerManagerMod.Core;

/// <summary>The config values the panel's Settings tab shows. Key is the panel key's name, "" for none.</summary>
public sealed record PanelSettings(string Key, bool AutoRefuel, int AutoRefuelMinutes, int ShedBelow);

/// <summary>
/// One change from the Settings tab. The page sends a setting's name and the value it wants; a number is pulled into
/// the setting's range and rounded to its step, so a stepper pressed past the end stays at the end.
/// </summary>
public abstract record SettingChange
{
    public const int MinutesLeast = 10;
    public const int MinutesMost = 1440;
    public const int MinutesStep = 10;
    public const int ShedMost = 90;
    public const int ShedStep = 5;

    private SettingChange()
    {
    }

    public sealed record AutoRefuel(bool IsOn) : SettingChange;

    public sealed record AutoRefuelMinutes(int Minutes) : SettingChange;

    public sealed record ShedBelow(int Percent) : SettingChange;

    /// <summary>The change a setting's name and value stand for, or null for a name or value that does not parse.</summary>
    public static SettingChange? Parse(string name, string value) => name switch
    {
        "autorefuel" => value switch
        {
            "1" => new AutoRefuel(true),
            "0" => new AutoRefuel(false),
            _ => null,
        },
        "autominutes" => TryNumber(value, out var minutes) ? new AutoRefuelMinutes(Stepped(minutes, MinutesStep, MinutesLeast, MinutesMost)) : null,
        "shedbelow" => TryNumber(value, out var percent) ? new ShedBelow(Stepped(percent, ShedStep, 0, ShedMost)) : null,
        _ => null,
    };

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static int Stepped(double value, int step, int least, int most)
    {
        var steps = Math.Round(Math.Clamp(value, least, most) / step, MidpointRounding.AwayFromZero);

        return (int)Math.Clamp(steps * step, least, most);
    }
}
