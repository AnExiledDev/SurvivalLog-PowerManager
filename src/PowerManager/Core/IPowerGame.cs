using System.Collections.Generic;

namespace PowerManagerMod.Core;

public enum FuelAdd
{
    Added,

    /// <summary>The generator had no free slot, so nothing was taken from storage.</summary>
    Full,

    /// <summary>The game would not take that stack (it is gone, or not fuel after all); the rest may still go in.</summary>
    Refused,

    /// <summary>The generator is gone or the game cannot be read.</summary>
    Gone,
}

/// <summary>
/// The game as the mod sees it. Each call that changes the game is one call of the game's own button handlers, made at
/// most once per frame. Every call fails soft: a failure reads as nothing read or nothing done.
/// </summary>
public interface IPowerGame
{
    /// <summary>Null with no game loaded or when the game cannot be read.</summary>
    PowerScene? Read();

    /// <summary>Every stack of a burnable item in home storage.</summary>
    IReadOnlyList<FuelStack> Fuel();

    /// <summary>An item's name in the game's language, for the fuel list.</summary>
    string ItemName(int configId);

    /// <summary>The generator's own on/off button. False when the call could not be made.</summary>
    bool Toggle(long generatorId);

    /// <summary>The generator's own auto-start setting, with the battery share it starts below.</summary>
    bool SetAutoStart(long generatorId, bool isOn, float at);

    /// <summary>One of the stack into the generator, as dragging it to the fuel tray does.</summary>
    FuelAdd AddFuel(long generatorId, long itemId);

    /// <summary>The device's switch as the game has it after the press, null when the call failed.</summary>
    bool? Switch(long deviceId, bool isOn);
}
