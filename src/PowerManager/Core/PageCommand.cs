using System;
using System.Globalization;

namespace PowerManagerMod.Core;

/// <summary>
/// What the panel and HUD button scripts ask for. They send "slmod|powermanager|" followed by a verb and its arguments
/// (a generator or device id, an item id, a setting and its value); anything else parses to Unknown so it can be logged
/// rather than acted on.
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|powermanager|";

    private PageCommand()
    {
    }

    /// <summary>The HUD button was clicked: open the panel, or close it when it is open.</summary>
    public sealed record Toggle : PageCommand;

    public sealed record Close : PageCommand;

    public sealed record Start(long Id) : PageCommand;

    public sealed record Stop(long Id) : PageCommand;

    public sealed record StartAll : PageCommand;

    public sealed record StopAll : PageCommand;

    public sealed record Auto(long Id, bool IsOn) : PageCommand;

    public sealed record AutoAll(bool IsOn) : PageCommand;

    /// <summary>The battery share a generator auto-starts below, already pulled into the game's 0.1 to 0.9.</summary>
    public sealed record AutoAt(long Id, float At) : PageCommand;

    public sealed record Refuel(long Id) : PageCommand;

    public sealed record RefuelAll : PageCommand;

    public sealed record Device(long Id, bool IsOn) : PageCommand;

    public sealed record Low(long Id, bool IsLow) : PageCommand;

    public sealed record FuelUp(int ConfigId) : PageCommand;

    public sealed record FuelDown(int ConfigId) : PageCommand;

    public sealed record FuelOff(int ConfigId) : PageCommand;

    public sealed record FuelAdd(int ConfigId) : PageCommand;

    /// <summary>A change on the panel's Settings tab.</summary>
    public sealed record Setting(SettingChange Change) : PageCommand;

    /// <summary>The page script just installed itself.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    public sealed record Resend : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public const float AutoAtLeast = 0.1f;
    public const float AutoAtMost = 0.9f;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        return (parts[0], parts.Length) switch
        {
            ("toggle", 1) => new Toggle(),
            ("close", 1) => new Close(),
            ("start", 2) when TryId(parts[1], out var id) => new Start(id),
            ("stop", 2) when TryId(parts[1], out var id) => new Stop(id),
            ("startall", 1) => new StartAll(),
            ("stopall", 1) => new StopAll(),
            ("auto", 3) when TryId(parts[1], out var id) && TryFlag(parts[2], out var isOn) => new Auto(id, isOn),
            ("autoall", 2) when TryFlag(parts[1], out var isOn) => new AutoAll(isOn),
            ("autoat", 3) when TryId(parts[1], out var id) && TryShare(parts[2], out var at) => new AutoAt(id, at),
            ("refuel", 2) when TryId(parts[1], out var id) => new Refuel(id),
            ("refuelall", 1) => new RefuelAll(),
            ("device", 3) when TryId(parts[1], out var id) && TryFlag(parts[2], out var isOn) => new Device(id, isOn),
            ("low", 3) when TryId(parts[1], out var id) && TryFlag(parts[2], out var isLow) => new Low(id, isLow),
            ("fuelup", 2) when TryConfigId(parts[1], out var configId) => new FuelUp(configId),
            ("fueldown", 2) when TryConfigId(parts[1], out var configId) => new FuelDown(configId),
            ("fueloff", 2) when TryConfigId(parts[1], out var configId) => new FuelOff(configId),
            ("fueladd", 2) when TryConfigId(parts[1], out var configId) => new FuelAdd(configId),
            ("set", 3) when SettingChange.Parse(parts[1], parts[2]) is { } change => new Setting(change),
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("resend", 1) => new Resend(),
            _ => new Unknown(message),
        };
    }

    private static bool TryId(string text, out long id) => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out id);

    private static bool TryConfigId(string text, out int configId) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out configId) && configId > 0;

    private static bool TryFlag(string text, out bool flag)
    {
        flag = text == "1";

        return text is "1" or "0";
    }

    /// <summary>Kept to the game's range and rounded to a tenth, the steps the panel offers.</summary>
    private static bool TryShare(string text, out float share)
    {
        share = 0f;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) return false;

        share = (float)Math.Round(Math.Clamp(value, AutoAtLeast, AutoAtMost), 1, MidpointRounding.AwayFromZero);

        return true;
    }
}
