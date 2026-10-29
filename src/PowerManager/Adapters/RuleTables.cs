using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Writes the battery and generator numbers into the game's electrical table (Config_FurnitureElectrical). The power
/// manager adds up every battery's Capacity for the base's storage, and every generation step reads a generator's
/// BasePower, so a number written there holds everywhere the game shows or uses it. No hook is needed. The table is
/// checked every few seconds so one the game loads again (a reload builds fresh rows with its own numbers) is written
/// again; a check of a table already written costs nothing but a pointer and a count.
/// </summary>
internal sealed class RuleTables
{
    private const float CheckSeconds = 2f;
    private const int GeneratorType = 3;

    private readonly float _batteryMultiplier;
    private readonly Action<string> _log;
    private readonly HashSet<IntPtr> _scaledBatteries = new();
    private readonly Dictionary<int, TableValue> _generators = new();
    private Multiplier _efficiency = Multiplier.GameAmount;
    private bool _isEfficiencyChanged = true;

    // The table last walked, held so the game cannot free it and build the next one at the same address.
    private Il2CppSystem.Collections.Generic.Dictionary<int, Config_FurnitureElectrical>? _walked;
    private int _walkedCount;
    private float _nextCheckAt;
    private bool _hasReportedFailure;

    /// <summary>The battery multiplier is read once, at the game start, as the game sizes its storage then.</summary>
    public RuleTables(float batteryMultiplier, Action<string> log)
    {
        _batteryMultiplier = batteryMultiplier;
        _log = log;
    }

    /// <summary>Called on the main thread at start and after each change of GeneratorEfficiency.</summary>
    public void UseEfficiency(Multiplier efficiency)
    {
        var complaint = efficiency.Complaint("GeneratorEfficiency");

        if (complaint != null) _log(complaint);

        _efficiency = efficiency;
        _isEfficiencyChanged = true;
        _nextCheckAt = 0f;
        _log(efficiency.IsGameAmount ? "generators make the game's power from their fuel" : $"generators make {efficiency.Value:0.##}x the game's power from their fuel");
    }

    public void Tick(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;

        try
        {
            Check();
        }
        catch (Exception ex)
        {
            if (_hasReportedFailure) return;

            _hasReportedFailure = true;
            _log($"writing the battery and generator table failed, they keep the game's numbers (logged once): {ex}");
        }
    }

    private void Check()
    {
        var changesBattery = _batteryMultiplier != 1f;

        // Nothing to write until a setting leaves the game's numbers; after that, a return to 1 writes the game's back.
        if (!changesBattery && _efficiency.IsGameAmount && _generators.Count == 0) return;

        var rows = GameSingleton.Existing<ConfigManager>()?._Config_FurnitureElectrical_Dict;

        if (rows == null) return;

        var count = rows.Count;

        if (count == 0) return;

        // Every row was written when this table was last walked, and walking it again makes a new object per row.
        if (!_isEfficiencyChanged && _walked != null && _walked.Pointer == rows.Pointer && count == _walkedCount) return;

        var batteries = 0;
        var generators = 0;

        foreach (var row in rows.Values)
        {
            if (row == null) continue;

            var type = row._ElectricalType_k__BackingField;

            if (type == BatteryCapacity.BatteryType && changesBattery && _scaledBatteries.Add(row.Pointer))
            {
                var game = row._Capacity_k__BackingField;
                var scaled = BatteryCapacity.Scale(type, game, _batteryMultiplier);

                if (scaled != game)
                {
                    row._Capacity_k__BackingField = scaled;
                    batteries++;
                }
            }
            else if (type == GeneratorType && WriteGenerator(row))
            {
                generators++;
            }
        }

        _walked = rows;
        _walkedCount = count;
        _isEfficiencyChanged = false;

        if (batteries > 0) _log($"{batteries} battery types now hold {_batteryMultiplier:0.##}x the game's power");
        if (generators > 0) _log($"{generators} generator types now make {_efficiency.Value:0.##}x the game's power");
    }

    /// <summary>Writes the row's power for the current setting; true when the row's number changed.</summary>
    private bool WriteGenerator(Config_FurnitureElectrical row)
    {
        var id = row._ID_k__BackingField;
        var current = row._BasePower_k__BackingField;
        TableValue? last = _generators.TryGetValue(id, out var seen) ? seen : null;
        var next = TableValue.Next(current, last, _efficiency);

        _generators[id] = next;

        if (next.Written == current) return false;

        row._BasePower_k__BackingField = next.Written;

        return true;
    }
}
