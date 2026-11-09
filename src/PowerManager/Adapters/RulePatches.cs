using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using ExiledToolkit.Game;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The Harmony hooks for the power rules: device power use, the city power cut, Blackout panic, how fast generators
/// burn fuel, and the battery table written just before the game counts it. Each hook goes in the first time its setting leaves the game's rule and stays in; back at the
/// game's rule, it lets the game's answer through. Every target was checked in GameAssembly.dll: no relative jump
/// or call in its first 14 bytes. Every dynamic event is built by DynamicEventManager.CreateDynamicEventArgs, whose
/// callers all handle it returning nothing, so a held-back power cut is simply not built and is offered again at the
/// next check. Static because Harmony patches are.
/// </summary>
internal static class RulePatches
{
    /// <summary>The Config_Buff ID of Blackout panic: morale drops every two hours until it is taken away.</summary>
    private const int BlackoutPanicBuff = 30002;

    private static readonly HashSet<string> LoggedOnce = new();
    private static PowerRules _rules = PowerRules.Game;
    private static Speed _burn = Speed.GamePace;
    private static RuleTables? _tables;
    private static Action<string> _log = _ => { };
    private static PatchKit? _hooks;
    private static bool _isPowerHooked;
    private static bool _isCutHooked;
    private static bool _isPanicHooked;
    private static bool _isBurnHooked;

    /// <summary>Called on the main thread at start and after each change.</summary>
    public static void Use(PatchKit hooks, PowerRules rules, Action<string> log)
    {
        _log = log;
        _rules = rules;

        if (rules.ChangesDevicePower && !_isPowerHooked)
        {
            _isPowerHooked = true;
            Hook(hooks, typeof(PowerManager), "GetEffectivePowerCost", new[] { typeof(Config_Furniture), typeof(long) }, postfix: nameof(ScalePowerCost));
        }

        if (rules.ChangesPowerCut && !_isCutHooked)
        {
            _isCutHooked = true;
            Hook(hooks, typeof(DynamicEventManager), nameof(DynamicEventManager.CreateDynamicEventArgs),
                new[] { typeof(DynamicEventConfig), typeof(bool), typeof(bool), typeof(bool) }, prefix: nameof(HoldPowerCut));
        }

        if (rules.NoBlackoutPanic && !_isPanicHooked)
        {
            _isPanicHooked = true;
            Hook(hooks, typeof(PowerManager), "SyncBlackoutPanicBuff", Type.EmptyTypes, prefix: nameof(ClearPanic));
        }

        log(rules.ChangesDevicePower ? $"devices use {rules.DevicePowerUse:0.##}x the game's power" : "devices use the game's power");
        log(rules.NoPowerCut ? "the city power is never cut"
            : rules.PowerCutDelay > 0 ? $"the city power cut comes {rules.PowerCutDelay} day(s) later than in the game"
            : "the city power cut comes as in the game");
        log(rules.NoBlackoutPanic ? "blackouts no longer bring Blackout panic" : "blackouts bring Blackout panic as in the game");
    }

    /// <summary>Called on the main thread at start and after each change of FuelBurn.</summary>
    public static void UseBurn(PatchKit hooks, Speed burn, Action<string> log)
    {
        _log = log;
        _burn = burn;

        var complaint = burn.Complaint("FuelBurn");

        if (complaint != null) log(complaint);

        if (!burn.IsGamePace && !_isBurnHooked)
        {
            _isBurnHooked = true;
            Hook(hooks, typeof(GeneratorFuelComponent), nameof(GeneratorFuelComponent.ConsumeFuel), new[] { typeof(float) }, prefix: nameof(ScaleBurn));
            Hook(hooks, typeof(GeneratorFuelComponent), nameof(GeneratorFuelComponent.GetRemainingGameSeconds), Type.EmptyTypes, postfix: nameof(ScaleTimeLeft));
        }

        log(burn.IsGamePace ? "generators burn fuel at the game's pace" : $"generators burn fuel {burn.Value:0.##}x as fast");
    }

    /// <summary>
    /// Called once at start. The game adds up the batteries' capacity in the same frame a world starts, before any live
    /// setting is heard, and cuts the save's stored power down to it; this hook writes the world's own BatteryMultiplier
    /// into the table first. Hooked even at the game's value, as a world may set its own.
    /// </summary>
    public static void UseTables(PatchKit hooks, RuleTables tables, Action<string> log)
    {
        _log = log;
        _tables = tables;
        Hook(hooks, typeof(PowerManager), "CalculateCapacity", new[] { typeof(Il2CppSystem.Collections.Generic.List<Furniture>) }, prefix: nameof(SyncBattery));
    }

    /// <summary>A hook that cannot go in is logged as "could not hook", and its rule stays the game's.</summary>
    private static void Hook(PatchKit hooks, Type type, string target, Type[] parameters, string? prefix = null, string? postfix = null)
    {
        _hooks = hooks;
        hooks.Patch(typeof(RulePatches), type, target, parameters, prefix, postfix, consequence: "it keeps the game's rules");
    }

    /// <summary>Runs inside the game's call; an escaping exception would reach native code.</summary>
    private static void ScalePowerCost(ref float __result)
    {
        try
        {
            __result *= _rules.DevicePowerUse;
        }
        catch (Exception ex)
        {
            _hooks?.Fail("scaling a device's power use, it uses the game's amount", ex);
        }
    }

    /// <summary>Runs inside the game's call for every event; a failure lets the event through.</summary>
    private static bool HoldPowerCut(DynamicEventConfig config, ref DynamicEventArgs? __result)
    {
        try
        {
            if (config == null) return true;

            var eventId = config._EventId_k__BackingField;

            // Every event the game builds passes through here, so the day is only read for the power cut.
            if (!PowerCutBlock.IsPowerCutEvent(eventId) || !PowerCutBlock.Blocks(eventId, GameDay(), _rules)) return true;

            __result = null;
            LogOnce("cut " + eventId, $"held back the city power cut, event {eventId} (logged once per event)");

            return false;
        }
        catch (Exception ex)
        {
            _hooks?.Fail("checking the power cut, it comes as in the game", ex);

            return true;
        }
    }

    /// <summary>The game day counted from 0, as the event table counts it; null before a run has loaded.</summary>
    private static int? GameDay() => GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager?.GetDay();

    /// <summary>
    /// Skips the game's panic check and takes a running panic away, so one already running clears at the next check (it
    /// runs every ten in-game minutes and on every power change). A failure keeps the game's rule.
    /// </summary>
    private static bool ClearPanic(PowerManager __instance)
    {
        try
        {
            if (!_rules.NoBlackoutPanic) return true;

            if (__instance == null || __instance._IsLocked_k__BackingField) return false;

            var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();
            var buffs = role == null ? null : AgentTools.GetAgentComponent<BuffComponent>(role);

            if (buffs == null || !buffs.IsBuffConfigExist(BlackoutPanicBuff)) return false;

            buffs.RemoveBuff(BlackoutPanicBuff);
            LogOnce("cleared", "took away a Blackout panic that was already running (logged once)");

            return false;
        }
        catch (Exception ex)
        {
            _hooks?.Fail("checking Blackout panic, it works as in the game", ex);

            return true;
        }
    }

    /// <summary>Runs inside the game's call; a failure leaves the table as it is until the next check.</summary>
    private static void SyncBattery()
    {
        try
        {
            _tables?.SyncBattery();
        }
        catch (Exception ex)
        {
            _hooks?.Fail("writing the battery table before the game counted it, it is written within seconds", ex);
        }
    }

    /// <summary>A generator takes fuel as it runs; taking more per stretch makes the tank run dry sooner.</summary>
    private static void ScaleBurn(ref float burnValueToConsume)
    {
        try
        {
            burnValueToConsume = Scaling.Burn(burnValueToConsume, _burn);
        }
        catch (Exception ex)
        {
            _hooks?.Fail("changing the fuel burn, it burns at the game's pace", ex);
        }
    }

    /// <summary>The time left the game shows (and the panel reads), divided by the same speed so it matches.</summary>
    private static void ScaleTimeLeft(ref float __result)
    {
        try
        {
            __result = Scaling.Duration(__result, _burn);
        }
        catch (Exception ex)
        {
            _hooks?.Fail("changing a generator's time left, it shows the game's", ex);
        }
    }

    private static void LogOnce(string key, string message)
    {
        lock (LoggedOnce)
        {
            if (!LoggedOnce.Add(key)) return;
        }

        _log(message);
    }
}
