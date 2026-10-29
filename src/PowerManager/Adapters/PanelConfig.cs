using System;
using BepInEx.Configuration;
using PowerManagerMod.Core;
using UnityEngine;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The config entries the panel reads and changes. The session reads them as it goes, so a change made here, on the
/// Mod Manager's Mods screen or in the file (with Exiled's Toolkit watching it) is used from the next frame on.
/// </summary>
internal sealed class PanelConfig
{
    private readonly ConfigEntry<KeyCode> _panelKey;
    private readonly ConfigEntry<bool> _autoRefuel;
    private readonly ConfigEntry<int> _autoRefuelMinutes;
    private readonly ConfigEntry<int> _shedBelow;
    private readonly ConfigEntry<string> _fuelOrder;
    private string? _parsedOrderText;
    private FuelOrder _order = FuelOrder.Parse(FuelOrder.Default);

    public PanelConfig(ConfigEntry<KeyCode> panelKey, ConfigEntry<bool> autoRefuel, ConfigEntry<int> autoRefuelMinutes, ConfigEntry<int> shedBelow, ConfigEntry<string> fuelOrder)
    {
        _panelKey = panelKey;
        _autoRefuel = autoRefuel;
        _autoRefuelMinutes = autoRefuelMinutes;
        _shedBelow = shedBelow;
        _fuelOrder = fuelOrder;
    }

    public KeyCode PanelKey => _panelKey.Value;

    /// <summary>The game minutes of fuel below which a generator is refuelled, 0 while auto-refuel is off.</summary>
    public int AutoRefuelMinutes => _autoRefuel.Value ? Math.Clamp(_autoRefuelMinutes.Value, SettingChange.MinutesLeast, SettingChange.MinutesMost) : 0;

    public int ShedBelow => Math.Clamp(_shedBelow.Value, 0, SettingChange.ShedMost);

    /// <summary>Parsed again only when the text changed.</summary>
    public FuelOrder Order
    {
        get
        {
            var text = _fuelOrder.Value;

            if (text != _parsedOrderText)
            {
                _parsedOrderText = text;
                _order = FuelOrder.Parse(text);
            }

            return _order;
        }
        set
        {
            _order = value;
            _parsedOrderText = value.ToString();
            _fuelOrder.Value = _parsedOrderText;
        }
    }

    public PanelSettings Read(string keyName) => new(keyName, _autoRefuel.Value, Math.Clamp(_autoRefuelMinutes.Value, SettingChange.MinutesLeast, SettingChange.MinutesMost), ShedBelow);

    /// <summary>Saves the change and returns the status line for it.</summary>
    public string Apply(SettingChange change, bool isChinese)
    {
        switch (change)
        {
            case SettingChange.AutoRefuel auto:
                _autoRefuel.Value = auto.IsOn;
                break;
            case SettingChange.AutoRefuelMinutes minutes:
                _autoRefuelMinutes.Value = minutes.Minutes;
                break;
            case SettingChange.ShedBelow shed:
                _shedBelow.Value = shed.Percent;
                break;
            default:
                throw new InvalidOperationException("unknown setting change " + change);
        }

        return Texts.Saved.Pick(isChinese);
    }
}
