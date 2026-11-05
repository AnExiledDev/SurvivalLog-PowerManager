using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using ExiledToolkit.Game;
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
    private const float LeastBattery = 0.1f;
    private const float MostBattery = 10f;

    private readonly Func<float> _readBattery;
    private readonly Action<string> _log;
    private readonly Dictionary<int, TableValue> _batteries = new();
    private readonly Dictionary<int, TableValue> _generators = new();
    private Multiplier _battery = Multiplier.GameAmount;
    private Multiplier _efficiency = Multiplier.GameAmount;
    private bool _isChanged = true;

    // The table last walked, held so the game cannot free it and build the next one at the same address.
    private Il2CppSystem.Collections.Generic.Dictionary<int, Config_FurnitureElectrical>? _walked;
    private int _walkedCount;
    private float _nextCheckAt;
    private bool _hasReportedFailure;

    /// <summary>
    /// <paramref name="readBattery"/> is the BatteryMultiplier setting as it is now. It is read at each check rather
    /// than waited for, because a world's own value is put in place as the world starts, a frame before a watcher of the
    /// setting hears of it, and the game adds up the batteries in that same frame (see <see cref="SyncBattery"/>).
    /// </summary>
    public RuleTables(Func<float> readBattery, Action<string> log)
    {
        _readBattery = readBattery;
        _log = log;
    }

    /// <summary>Called on the main thread at start and after each change of GeneratorEfficiency.</summary>
    public void UseEfficiency(Multiplier efficiency)
    {
        var complaint = efficiency.Complaint("GeneratorEfficiency");

        if (complaint != null) _log(complaint);

        _efficiency = efficiency;
        _isChanged = true;
        _nextCheckAt = 0f;
        _log(efficiency.IsGameAmount ? "generators make the game's power from their fuel" : $"generators make {efficiency.Value:0.##}x the game's power from their fuel");
    }

    /// <summary>Called on the main thread after a change of BatteryMultiplier, so the next frame writes it.</summary>
    public void CheckSoon() => _nextCheckAt = 0f;

    /// <summary>
    /// Called by the game each time it adds up the batteries' capacity, just before it does. Writes the battery rows
    /// first when the setting has changed since they were written: on a save's first count a world's own value would
    /// otherwise not be in yet, and the game would cut the stored power down to the last world's batteries. Costs one
    /// read of the setting when nothing changed.
    /// </summary>
    public void SyncBattery()
    {
        if (_readBattery().Equals(_battery.Requested)) return;

        Check();
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
        var requested = _readBattery();

        if (!requested.Equals(_battery.Requested)) UseBattery(Multiplier.From(requested, LeastBattery, MostBattery));

        // Nothing to write until a setting leaves the game's numbers; after that, a return to 1 writes the game's back.
        if (_battery.IsGameAmount && _efficiency.IsGameAmount && _batteries.Count == 0 && _generators.Count == 0) return;

        var rows = GameSingleton.Existing<ConfigManager>()?._Config_FurnitureElectrical_Dict;

        if (rows == null) return;

        var count = rows.Count;

        if (count == 0) return;

        // Every row was written when this table was last walked, and walking it again makes a new object per row.
        if (!_isChanged && _walked != null && _walked.Pointer == rows.Pointer && count == _walkedCount) return;

        var batteries = 0;
        var generators = 0;

        foreach (var row in rows.Values)
        {
            if (row == null) continue;

            var type = row._ElectricalType_k__BackingField;

            if (type == GeneratorType)
            {
                if (WriteGenerator(row)) generators++;
            }
            else if (WriteBattery(row, type))
            {
                batteries++;
            }
        }

        _walked = rows;
        _walkedCount = count;
        _isChanged = false;

        if (batteries > 0) _log($"{batteries} battery types now hold {_battery.Value:0.##}x the game's power");
        if (generators > 0) _log($"{generators} generator types now make {_efficiency.Value:0.##}x the game's power");
    }

    private void UseBattery(Multiplier battery)
    {
        var complaint = battery.Complaint("BatteryMultiplier");

        if (complaint != null) _log(complaint);

        _battery = battery;
        _isChanged = true;
        _log(battery.IsGameAmount ? "batteries hold the game's power" : $"batteries hold {battery.Value:0.##}x the game's power");
    }

    /// <summary>Writes a battery row's capacity for the current setting; true when the row's number changed. Other rows are left alone.</summary>
    private bool WriteBattery(Config_FurnitureElectrical row, int type)
    {
        var id = row._ID_k__BackingField;
        var current = row._Capacity_k__BackingField;
        TableValue? last = _batteries.TryGetValue(id, out var seen) ? seen : null;

        if (BatteryCapacity.Next(type, current, last, _battery) is not { } next) return false;

        _batteries[id] = next;

        if (next.Written == current) return false;

        row._Capacity_k__BackingField = next.Written;

        return true;
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
