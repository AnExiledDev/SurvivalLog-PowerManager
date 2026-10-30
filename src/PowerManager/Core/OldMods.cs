using System;

namespace PowerManagerMod.Core;

/// <summary>
/// Game Rules before 0.7.0 and Machine Speed before 0.7.0 still change the power settings this mod took over. With one
/// of them installed, both mods would apply the same setting and the player would get it twice (half of half the
/// device power, four times the battery), so this mod leaves that mod's settings to it until it is updated.
/// </summary>
public static class OldMods
{
    public const string GameRulesGuid = "com.dev.survivallog.gamerules";
    public const string MachineSpeedGuid = "com.dev.survivallog.machinespeed";

    private static readonly Version FirstWithoutPower = new(0, 7, 0);

    /// <summary>True when that version still has the power settings. No version (not installed) or one that cannot be read is not old.</summary>
    public static bool IsOld(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;

        var core = version.Split('-', '+')[0];

        return Version.TryParse(core, out var parsed) && parsed < FirstWithoutPower;
    }

    /// <summary>The names of the mods to update, "" when none is old.</summary>
    public static string Names(bool isGameRulesOld, bool isMachineSpeedOld) => (isGameRulesOld, isMachineSpeedOld) switch
    {
        (true, true) => "Game Rules and Machine Speed",
        (true, false) => "Game Rules",
        (false, true) => "Machine Speed",
        _ => "",
    };
}
