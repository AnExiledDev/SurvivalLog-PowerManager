using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using PowerManagerMod.Core;
using Il2CppInterop.Runtime;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Changes what solar panels make. PowerManager.CalcSolarOutput (rva 0x274cd10) works out one panel's output for the
/// power grid, the panel's hover text and the facilities page alike, so one hook covers all three. It returns a
/// private struct through a hidden pointer, which Harmony cannot patch safely, so it is detoured natively with the
/// hook library BepInEx already ships (core/dobby.dll). Its first 15 bytes are three `mov [rsp+x],reg` saves with no
/// relative operand; the detour is only placed when those exact bytes are there, so a game update or another hook
/// leaves the panels alone instead of breaking them.
/// </summary>
internal static unsafe class SolarDetour
{
    private static readonly byte[] ExpectedPrologue = { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18 };

    // The native layout of PowerManager.SolarOutput.
    private const int PowerOffset = 0x0;
    private const int AreaLightMulOffset = 0x10;
    private const int EfficiencyOffset = 0x14;
    private const int ReasonOffset = 0x18;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CalcSolarOutput(IntPtr result, IntPtr furniture, IntPtr elecConfig, IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DobbyHook(IntPtr target, IntPtr replacement, out IntPtr original);

    private static PowerRules _settings = PowerRules.Game;
    private static Action<string> _log = _ => { };
    private static CalcSolarOutput? _original;
    private static bool _isTried;
    private static bool _hasLoggedChange;
    private static int _basePowerOffset;
    private static readonly HashSet<string> LoggedOnce = new();

    // Kept in a static field so the collector never frees the function the game calls.
    private static readonly CalcSolarOutput Replacement = OnCalcSolarOutput;

    /// <summary>
    /// Called on the main thread at start and after each change. The detour is placed the first time the panels change and
    /// stays; back at the game's numbers, it hands the game's own output through.
    /// </summary>
    public static void Use(PowerRules settings, Action<string> log)
    {
        _settings = settings;
        _log = log;

        if (!settings.ChangesSolar)
        {
            log("solar panels make the game's output");

            return;
        }

        if (!_isTried)
        {
            _isTried = true;

            Place(log);
        }

        if (_original != null) log($"solar panels make {settings.SolarMultiplier:0.##}x the game's output, and {settings.BadWeatherSolar:0.##} of a cloudy day's in storms and heavy rain");
    }

    private static void Place(Action<string> log)
    {
        try
        {
            var target = TargetAddress();

            if (!HasExpectedPrologue(target))
            {
                log("solar panels keep the game's output: CalcSolarOutput does not start as expected (a game update, or another mod hooks it)");

                return;
            }

            // Found before the detour goes in, so every call already has it.
            _basePowerOffset = FindBasePowerOffset();

            var hook = DobbyHookExport();

            if (hook(target, Marshal.GetFunctionPointerForDelegate(Replacement), out var original) != 0 || original == IntPtr.Zero)
            {
                log("solar panels keep the game's output: the hook library refused the hook");

                return;
            }

            _original = Marshal.GetDelegateForFunctionPointer<CalcSolarOutput>(original);
        }
        catch (Exception ex)
        {
            log($"solar panels keep the game's output: placing the hook failed: {ex.Message}");
        }
    }

    /// <summary>The method's native code, read from the method info the interop assembly keeps for it.</summary>
    private static IntPtr TargetAddress()
    {
        foreach (var field in typeof(PowerManager).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.FieldType != typeof(IntPtr) || !field.Name.StartsWith("NativeMethodInfoPtr_CalcSolarOutput", StringComparison.Ordinal)) continue;

            var methodInfo = (IntPtr)(field.GetValue(null) ?? IntPtr.Zero);

            if (methodInfo == IntPtr.Zero) break;

            return Marshal.ReadIntPtr(methodInfo);
        }

        throw new MissingMethodException("PowerManager.CalcSolarOutput");
    }

    private static bool HasExpectedPrologue(IntPtr target)
    {
        if (target == IntPtr.Zero) return false;

        var code = (byte*)target;

        for (var i = 0; i < ExpectedPrologue.Length; i++)
        {
            if (code[i] != ExpectedPrologue[i]) return false;
        }

        return true;
    }

    private static DobbyHook DobbyHookExport()
    {
        var library = NativeLibrary.Load(Path.Combine(Paths.BepInExRootPath, "core", "dobby.dll"));

        return Marshal.GetDelegateForFunctionPointer<DobbyHook>(NativeLibrary.GetExport(library, "DobbyHook"));
    }

    /// <summary>Runs inside the game's own call; nothing may escape into native code, and the result pointer always returns.</summary>
    private static IntPtr OnCalcSolarOutput(IntPtr result, IntPtr furniture, IntPtr elecConfig, IntPtr method)
    {
        var returned = _original!(result, furniture, elecConfig, method);

        try
        {
            Adjust(result, elecConfig);
        }
        catch (Exception ex)
        {
            LogOnce("solar failure", $"changing a solar panel's output failed, it keeps the game's (logged once): {ex}");
        }

        return returned;
    }

    /// <summary>
    /// Runs for every panel each time the grid is worked out, so nothing here makes an object: the config's base power
    /// and the weather reason are read in place rather than through interop wrappers and a copied string.
    /// </summary>
    private static void Adjust(IntPtr result, IntPtr elecConfig)
    {
        // Back at the game's numbers the detour stays in but changes nothing, so it reads nothing either.
        if (!_settings.ChangesSolar || result == IntPtr.Zero || elecConfig == IntPtr.Zero) return;

        var output = (byte*)result;
        var reasonPointer = *(IntPtr*)(output + ReasonOffset);
        var reading = new SolarReading(
            Power: *(float*)(output + PowerOffset),
            BasePower: BasePowerOf(elecConfig),
            AreaLightMul: *(float*)(output + AreaLightMulOffset),
            Efficiency: *(float*)(output + EfficiencyOffset),
            IsBadWeather: reasonPointer != IntPtr.Zero && SolarRules.IsBadWeatherReason(
                new ReadOnlySpan<char>((char*)(void*)IL2CPP.il2cpp_string_chars(reasonPointer), IL2CPP.il2cpp_string_length(reasonPointer))));
        var adjusted = SolarRules.Apply(reading, _settings);

        *(float*)(output + PowerOffset) = adjusted.Power;

        // The game shows a reason only for a panel making nothing, and a null reason is its own "running".
        if (adjusted.ClearsBadWeather) *(IntPtr*)(output + ReasonOffset) = IntPtr.Zero;

        if (adjusted.Power != reading.Power && !_hasLoggedChange)
        {
            _hasLoggedChange = true;
            LogOnce("solar", $"first changed solar output: {reading.Power:0.##} in the game, {adjusted.Power:0.##} with the mod");
        }
    }

    /// <summary>The config's BasePower, read from its field; through the wrapper it is a new object and a boxed read.</summary>
    private static float BasePowerOf(IntPtr elecConfig) =>
        _basePowerOffset > 0 ? *(float*)((byte*)elecConfig + _basePowerOffset) : new Config_FurnitureElectrical(elecConfig).BasePower;

    /// <summary>0 when the field is not found, and BasePower is then read through the wrapper as before.</summary>
    private static int FindBasePowerOffset()
    {
        try
        {
            var field = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Config_FurnitureElectrical>.NativeClassPtr, "<BasePower>k__BackingField");

            return field == IntPtr.Zero ? 0 : (int)IL2CPP.il2cpp_field_get_offset(field);
        }
        catch (Exception)
        {
            return 0;
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
