using System.Collections.Generic;

namespace PowerManagerMod.Core;

/// <summary>
/// The home's power as one read of the game sees it. Stored, capacity, generation and consumption are the game's own
/// numbers, in its power units per 10-minute tick. IsOutage is the city power being cut, the only time the battery and
/// generators matter. IsLocked is the game holding every switch (story scenes), when nothing may be pressed.
/// </summary>
public sealed record Battery(float Stored, float Capacity, float Generation, float Consumption, bool IsOutage, bool IsLocked)
{
    /// <summary>Charge as a whole percent, 0 with no battery built.</summary>
    public int Percent => Capacity > 0f ? (int)System.Math.Round(System.Math.Clamp(Stored / Capacity, 0f, 1f) * 100f) : 0;
}

/// <summary>
/// One fuel generator. FuelSeconds is the game time its fuel lasts at the current burn. AutoStartAt is the battery share
/// (0.1 to 0.9) below which the game starts it on its own while AutoStart is on.
/// </summary>
public sealed record Generator(long Id, string Name, bool IsOn, int FuelSlots, int MaxFuelSlots, float FuelSeconds, bool AutoStart, float AutoStartAt)
{
    public bool HasFuel => FuelSlots > 0;

    public bool IsFull => FuelSlots >= MaxFuelSlots;
}

/// <summary>
/// One powered device with its own switch. Watts is the game's use per tick while it is on. IsManaged is an outdoor
/// defense that Defense Manager switches on its own, which this mod leaves alone so the two never fight over it.
/// </summary>
public sealed record Device(long Id, DeviceKey Key, string Name, bool IsOn, float Watts, bool IsManaged = false);

/// <summary>One stack of a burnable item in home storage. ItemId is the stack's own id, which the game burns one of at a time.</summary>
public readonly record struct FuelStack(long ItemId, int ConfigId, int Count);

/// <summary>A load of a saved game, by the game objects behind it, so a reload is told apart from the same game going on.</summary>
public readonly record struct GameLoad(long World, long Save);

public sealed record PowerScene(Battery Battery, IReadOnlyList<Generator> Generators, IReadOnlyList<Device> Devices, GameLoad Load);
