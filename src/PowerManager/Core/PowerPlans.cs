using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerManagerMod.Core;

public enum ForecastKind
{
    /// <summary>The city power is on, so the battery is not being used.</summary>
    CityPower,
    NoBattery,
    Steady,
    Draining,
    Charging,
    Empty,
    Full,
}

/// <summary>Where the battery is heading and, while it drains or charges, in how many game minutes it gets there.</summary>
public readonly record struct Forecast(ForecastKind Kind, float GameMinutes)
{
    /// <summary>The game settles power once every 10 game minutes, by generation less consumption.</summary>
    public const float MinutesPerTick = 10f;

    public static Forecast Of(Battery battery)
    {
        if (!battery.IsOutage) return new Forecast(ForecastKind.CityPower, 0f);
        if (battery.Capacity <= 0f) return new Forecast(ForecastKind.NoBattery, 0f);

        var net = battery.Generation - battery.Consumption;

        if (net < 0f) return battery.Stored <= 0f ? new Forecast(ForecastKind.Empty, 0f) : new Forecast(ForecastKind.Draining, battery.Stored / -net * MinutesPerTick);
        if (net > 0f) return battery.Stored >= battery.Capacity ? new Forecast(ForecastKind.Full, 0f) : new Forecast(ForecastKind.Charging, (battery.Capacity - battery.Stored) / net * MinutesPerTick);

        return new Forecast(ForecastKind.Steady, 0f);
    }
}

/// <summary>The switches load shedding wants pressed: devices to switch off to save the battery, and ones it gives back.</summary>
public sealed record ShedPlan(IReadOnlyList<Device> SwitchOff, IReadOnlyList<Device> SwitchOn)
{
    public static readonly ShedPlan Nothing = new(Array.Empty<Device>(), Array.Empty<Device>());

    public bool IsEmpty => SwitchOff.Count == 0 && SwitchOn.Count == 0;
}

/// <summary>
/// Load shedding: while the city power is out and the battery is below the player's level, the devices marked low
/// priority are switched off. They are switched back on once the battery is 10 points above that level, the city power
/// is back, or shedding is turned off. Only devices this mod switched off come back on, never one the player turned off.
/// </summary>
public static class Shedding
{
    public const int RestoreMargin = 10;

    /// <param name="shedBelow">Battery percent to shed below; 0 turns shedding off.</param>
    /// <param name="low">The devices the player marked low priority.</param>
    /// <param name="shed">The devices this mod has switched off and not yet given back.</param>
    public static ShedPlan Plan(Battery battery, IReadOnlyList<Device> devices, IReadOnlyCollection<DeviceKey> low, IReadOnlyCollection<DeviceKey> shed, int shedBelow)
    {
        if (battery.IsLocked) return ShedPlan.Nothing;

        var isRunning = shedBelow > 0 && battery.IsOutage && battery.Capacity > 0f;
        var percent = battery.Percent;

        if (isRunning && percent < shedBelow) return new ShedPlan(Pick(devices, device => device.IsOn && low.Contains(device.Key)), Array.Empty<Device>());

        var isOver = !isRunning || percent >= shedBelow + RestoreMargin;

        if (!isOver || shed.Count == 0) return ShedPlan.Nothing;

        return new ShedPlan(Array.Empty<Device>(), Pick(devices, device => !device.IsOn && shed.Contains(device.Key)));
    }

    private static List<Device> Pick(IReadOnlyList<Device> devices, Func<Device, bool> wanted)
    {
        var picked = new List<Device>();

        foreach (var device in devices)
        {
            if (!device.IsManaged && wanted(device)) picked.Add(device);
        }

        return picked;
    }
}

public static class AutoRefuel
{
    /// <summary>
    /// A generator is topped up when its fuel lasts less than the player's minutes and it is running or set to start
    /// on its own. One that is off with auto-start off is left as the player left it.
    /// </summary>
    public static bool Wants(Generator generator, int minutes) =>
        minutes > 0 && !generator.IsFull && (generator.IsOn || generator.AutoStart) && generator.FuelSeconds < minutes * 60f;
}
