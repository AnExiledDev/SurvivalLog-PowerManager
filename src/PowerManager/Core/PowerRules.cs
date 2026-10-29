using System;

namespace PowerManagerMod.Core;

/// <summary>
/// The power rules the mod takes in from its config. A field at its game value changes nothing. SolarMultiplier and
/// BatteryMultiplier are times the game's; BadWeatherSolar is a share of a cloudy day's light; DevicePowerUse is times
/// the game's use; PowerCutDelay is in days.
/// </summary>
public sealed record PowerRules(
    float SolarMultiplier,
    float BadWeatherSolar,
    float BatteryMultiplier,
    float DevicePowerUse,
    int PowerCutDelay,
    bool NoPowerCut,
    bool NoBlackoutPanic)
{
    public static readonly PowerRules Game = new(1f, 0f, 1f, 1f, 0, false, false);

    public bool ChangesSolar => SolarMultiplier != 1f || BadWeatherSolar > 0f;

    public bool ChangesBattery => BatteryMultiplier != 1f;

    public bool ChangesDevicePower => DevicePowerUse != 1f;

    public bool ChangesPowerCut => NoPowerCut || PowerCutDelay > 0;
}

/// <summary>
/// One solar panel's output as the game worked it out: Power = LightValue x BasePower x AreaLightMul x Efficiency,
/// by day only. LightValue is the weather's light (2 sunny, 1 cloudy or rain, 0 in storms and heavy rain).
/// </summary>
public readonly record struct SolarReading(float Power, float BasePower, float AreaLightMul, float Efficiency, bool IsBadWeather);

/// <summary>The output to report instead. <see cref="ClearsBadWeather"/> when the panel now runs despite the weather.</summary>
public readonly record struct SolarResult(float Power, bool ClearsBadWeather);

public static class SolarRules
{
    /// <summary>The game's reason text for a panel stopped by the weather, compared in place without copying it out.</summary>
    public static bool IsBadWeatherReason(ReadOnlySpan<char> reason) => reason.SequenceEqual("bad_weather".AsSpan());

    /// <summary>
    /// Bad weather: the game gives nothing, so the panel gets <see cref="PowerRules.BadWeatherSolar"/> of a cloudy
    /// day's light; a panel standing where no light reaches (AreaLightMul 0) still makes nothing. Then every output is
    /// scaled.
    /// </summary>
    public static SolarResult Apply(SolarReading reading, PowerRules rules)
    {
        var power = reading.Power;
        var clearsBadWeather = false;

        if (reading.IsBadWeather && rules.BadWeatherSolar > 0f)
        {
            power = rules.BadWeatherSolar * reading.BasePower * reading.AreaLightMul * reading.Efficiency;
            clearsBadWeather = power > 0f;
        }

        return new SolarResult(power * rules.SolarMultiplier, clearsBadWeather);
    }
}

public static class BatteryCapacity
{
    /// <summary>The game's electrical type for a battery, the only type it counts toward stored power.</summary>
    public const int BatteryType = 5;

    /// <summary>A battery's capacity times the player's multiplier. Every other electrical device keeps its own number.</summary>
    public static float Scale(int electricalType, float gameCapacity, float multiplier) =>
        electricalType == BatteryType ? gameCapacity * multiplier : gameCapacity;
}

/// <summary>
/// Which story events the power-cut rules stop. The game cuts the city power with the "power cut begins" event on day
/// 14; the variants are the same event in the game's other story lines.
/// </summary>
public static class PowerCutBlock
{
    public const int GamePowerCutDay = 14;

    private static readonly int[] PowerCutEvents = { 40019, 52019, 62019 };

    public static bool IsPowerCutEvent(int eventId) => Array.IndexOf(PowerCutEvents, eventId) >= 0;

    /// <summary>
    /// Whether the event must wait. NoPowerCut stops it for good; a delay stops it until the game day reaches 14 plus
    /// the delay. A day that cannot be read lets the event go ahead.
    /// </summary>
    public static bool Blocks(int eventId, int? day, PowerRules rules)
    {
        if (!IsPowerCutEvent(eventId)) return false;
        if (rules.NoPowerCut) return true;

        return rules.PowerCutDelay > 0 && day is { } today && today < GamePowerCutDay + rules.PowerCutDelay;
    }
}
