using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The Harmony hooks for the power rules: device power use, the city power cut, Blackout panic and how fast
/// generators burn fuel. Each hook goes in the first time its setting leaves the game's rule and stays in; back at the
/// game's rule, it lets the game's own answer through. Every target was checked in GameAssembly.dll: no relative jump
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
    private static Action<string> _log = _ => { };
    private static bool _isPowerHooked;
    private static bool _isCutHooked;
    private static bool _isPanicHooked;
    private static bool _isBurnHooked;

    /// <summary>Called on the main thread at start and after each change.</summary>
    public static void Use(Harmony harmony, PowerRules rules, Action<string> log)
    {
        _log = log;
        _rules = rules;

        if (rules.ChangesDevicePower && !_isPowerHooked)
        {
            _isPowerHooked = true;
            Hook(harmony, typeof(PowerManager), "GetEffectivePowerCost", new[] { typeof(Config_Furniture), typeof(long) }, postfix: nameof(ScalePowerCost));
        }

        if (rules.ChangesPowerCut && !_isCutHooked)
        {
            _isCutHooked = true;
            Hook(harmony, typeof(DynamicEventManager), nameof(DynamicEventManager.CreateDynamicEventArgs),
                new[] { typeof(DynamicEventConfig), typeof(bool), typeof(bool), typeof(bool) }, prefix: nameof(HoldPowerCut));
        }

        if (rules.NoBlackoutPanic && !_isPanicHooked)
        {
            _isPanicHooked = true;
            Hook(harmony, typeof(PowerManager), "SyncBlackoutPanicBuff", Type.EmptyTypes, prefix: nameof(ClearPanic));
        }

        log(rules.ChangesDevicePower ? $"devices use {rules.DevicePowerUse:0.##}x the game's power" : "devices use the game's power");
        log(rules.NoPowerCut ? "the city power is never cut"
            : rules.PowerCutDelay > 0 ? $"the city power cut comes {rules.PowerCutDelay} day(s) later than in the game"
            : "the city power cut comes as in the game");
        log(rules.NoBlackoutPanic ? "blackouts no longer bring Blackout panic" : "blackouts bring Blackout panic as in the game");
    }

    /// <summary>Called on the main thread at start and after each change of FuelBurn.</summary>
    public static void UseBurn(Harmony harmony, Speed burn, Action<string> log)
    {
        _log = log;
        _burn = burn;

        var complaint = burn.Complaint("FuelBurn");

        if (complaint != null) log(complaint);

        if (!burn.IsGamePace && !_isBurnHooked)
        {
            _isBurnHooked = true;
            Hook(harmony, typeof(GeneratorFuelComponent), nameof(GeneratorFuelComponent.ConsumeFuel), new[] { typeof(float) }, prefix: nameof(ScaleBurn));
            Hook(harmony, typeof(GeneratorFuelComponent), nameof(GeneratorFuelComponent.GetRemainingGameSeconds), Type.EmptyTypes, postfix: nameof(ScaleTimeLeft));
        }

        log(burn.IsGamePace ? "generators burn fuel at the game's pace" : $"generators burn fuel {burn.Value:0.##}x as fast");
    }

    private static void Hook(Harmony harmony, Type type, string target, Type[] parameters, string? prefix = null, string? postfix = null)
    {
        try
        {
            var method = AccessTools.Method(type, target, parameters) ?? throw new MissingMethodException($"{type.Name}.{target}");

            harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(RulePatches), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(RulePatches), postfix));
        }
        catch (Exception ex)
        {
            _log($"{HookFailure.Prefix} {type.Name}.{target}, it keeps the game's rules: {ex.Message}");
        }
    }

    /// <summary>Runs inside the game's own call; an escaping exception would reach native code.</summary>
    private static void ScalePowerCost(ref float __result)
    {
        try
        {
            __result *= _rules.DevicePowerUse;
        }
        catch (Exception ex)
        {
            LogOnce("power failure", $"scaling a device's power use failed, it uses the game's amount (logged once): {ex}");
        }
    }

    /// <summary>Runs inside the game's own call for every event; a failure lets the event through.</summary>
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
            LogOnce("cut failure", $"checking the power cut failed, it comes as in the game (logged once): {ex}");

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
            LogOnce("panic failure", $"checking Blackout panic failed, it works as in the game (logged once): {ex}");

            return true;
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
            LogOnce("burn failure", $"changing the fuel burn failed, it burns at the game's pace (logged once): {ex}");
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
            LogOnce("time left failure", $"changing a generator's time left failed, it shows the game's (logged once): {ex}");
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
