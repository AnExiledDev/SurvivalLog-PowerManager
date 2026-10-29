using System;
using System.Collections.Generic;

namespace PowerManagerMod.Core;

/// <summary>One setting carried over: where it lives in this mod's config and the text it had in the old one.</summary>
public readonly record struct CarriedSetting(string Section, string Key, string Value);

/// <summary>
/// Reads the settings this mod took over from Game Rules and Machine Speed out of their config files, so a player who
/// had changed them keeps their values. Only the lines are read; the old files are never written.
/// </summary>
public static class OldConfig
{
    public const string GameRulesFile = "com.dev.survivallog.gamerules.cfg";
    public const string MachineSpeedFile = "com.dev.survivallog.machinespeed.cfg";

    private static readonly (string FromSection, string FromKey, string ToSection, string ToKey)[] FromGameRules =
    {
        ("Power", "BatteryMultiplier", "Power", "BatteryMultiplier"),
        ("Power", "DevicePowerUse", "Power", "DevicePowerUse"),
        ("Power", "PowerCutDelay", "Power", "PowerCutDelay"),
        ("Power", "NoPowerCut", "Power", "NoPowerCut"),
        ("Power", "NoBlackoutPanic", "Power", "NoBlackoutPanic"),
        ("Solar", "SolarMultiplier", "Solar", "SolarMultiplier"),
        ("Solar", "BadWeatherOutput", "Solar", "BadWeatherOutput"),
    };

    private static readonly (string FromSection, string FromKey, string ToSection, string ToKey)[] FromMachineSpeed =
    {
        ("Amounts", "GeneratorEfficiency", "Generators", "GeneratorEfficiency"),
        ("Machines", "FuelBurn", "Generators", "FuelBurn"),
    };

    /// <summary>
    /// Every "Section/Key = value" line of a BepInEx config file. Comments, blank lines and lines without "=" are
    /// skipped; a key seen twice keeps its last value, as BepInEx does.
    /// </summary>
    public static Dictionary<string, string> Read(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = "";

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line[0] == '#') continue;

            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                section = line.Substring(1, line.Length - 2).Trim();

                continue;
            }

            var equals = line.IndexOf('=');

            if (equals <= 0) continue;

            values[section + "/" + line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
        }

        return values;
    }

    /// <summary>Whether a config file's text has any setting in the section, so a file with none is a first start.</summary>
    public static bool HasSection(string text, string section)
    {
        foreach (var key in Read(text).Keys)
        {
            if (key.StartsWith(section + "/", StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>The settings to carry over from the two old files' text; a file that is missing is null.</summary>
    public static List<CarriedSetting> Carried(string? gameRules, string? machineSpeed)
    {
        var carried = new List<CarriedSetting>();

        Collect(carried, gameRules, FromGameRules);
        Collect(carried, machineSpeed, FromMachineSpeed);

        return carried;
    }

    private static void Collect(List<CarriedSetting> carried, string? text, (string FromSection, string FromKey, string ToSection, string ToKey)[] map)
    {
        if (text is null) return;

        var values = Read(text);

        foreach (var (fromSection, fromKey, toSection, toKey) in map)
        {
            if (values.TryGetValue(fromSection + "/" + fromKey, out var value) && value.Length > 0) carried.Add(new CarriedSetting(toSection, toKey, value));
        }
    }
}
