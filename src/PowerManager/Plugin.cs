using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PowerManagerMod.Adapters;
using PowerManagerMod.Core;
using ExiledToolkit;
using UnityEngine;

namespace PowerManagerMod;

[BepInPlugin(Guid, "Power Manager", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, OptionalLive.ToolkitVersion)]
// Loaded after these when they are installed, so their versions can be read (see OldMods).
[BepInDependency(OldMods.GameRulesGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(OldMods.MachineSpeedGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.powermanager";
    private const string Version = "0.2.2";
    private const string DefenseManagerGuid = "com.dev.survivallog.defensemanager";
    private const string DevicesFileName = Guid + ".devices.txt";
    private const string GeneratorsSection = "Generators";

    private const string RateRange = "From 0.1 to 100: 0 or less counts as 1, between 0 and 0.1 counts as 0.1, more than 100 counts as 100.";
    private const string RateRangeChinese = "范围 0.1 到 100：0 或更小按 1 计算，0 到 0.1 之间按 0.1 计算，大于 100 按 100 计算。";

    public override void Load()
    {
        var log = new PowerLog(Log, Path.Combine(Paths.BepInExRootPath, "power-manager.log"));
        var isFirstStart = IsFirstStart(log.Write);

        OptionalUiSize.Bind(Config);

        var panelKey = Config.Bind("Controls", "PanelKey", Enum.Parse<KeyCode>(Hotkey.DefaultKey),
            "Key that opens and closes the Power Manager panel. Unbound by default: the lightning button in the bottom-left corner of the screen opens the panel. Set a key here to open it from the keyboard too.\n" +
            "打开和关闭 Power Manager 面板的按键。默认不绑定：屏幕左下角的闪电按钮可以打开面板。在这里设置按键后，也可以用键盘打开。");
        var autoRefuel = Config.Bind(GeneratorsSection, "AutoRefuel", false,
            "Refuel generators by themselves from home storage when their fuel runs below AutoRefuelMinutes, using the fuel order. Off by default.\n" +
            "发电机燃料低于 AutoRefuelMinutes 时，自动按燃料顺序从家里的储物中添加燃料。默认关闭。");
        var autoRefuelMinutes = Config.Bind(GeneratorsSection, "AutoRefuelMinutes", 60, new ConfigDescription(
            "With AutoRefuel on, a generator with less than this many in-game minutes of fuel left is refuelled. 60 by default, from 10 to 1440.\n" +
            "开启 AutoRefuel 时，剩余燃料少于这么多游戏分钟的发电机会被添加燃料。默认 60，可设为 10 到 1440。",
            new AcceptableValueRange<int>(SettingChange.MinutesLeast, SettingChange.MinutesMost)));
        var efficiency = Config.Bind(GeneratorsSection, "GeneratorEfficiency", 1f,
            $"How much power fuel generators make from the same fuel: 2 is twice the power from each unit of fuel, 0.5 is half. How fast they burn fuel is FuelBurn. {RateRange}\n" +
            $"燃油发电机消耗相同燃料时的发电量：2 为每份燃料发出两倍电力，0.5 为一半。燃料消耗速度由 FuelBurn 设置。{RateRangeChinese}");
        var fuelBurn = Config.Bind(GeneratorsSection, "FuelBurn", 1f,
            "How fast fuel generators burn fuel: 2 burns it twice as fast, 0.5 makes it last twice as long. From 0.25 to 100000; 0 or less counts as 1.\n" +
            "燃油发电机消耗燃料的速度：2 为两倍消耗，0.5 让燃料多用一倍时间。范围 0.25 到 100000，0 或更小按 1 计算。");
        var shedBelow = Config.Bind("Shedding", "ShedBelow", 20, new ConfigDescription(
            "When the battery falls below this percent, devices marked low priority in the panel are switched off, and switched back on once it is 10 points above. 0 turns this off. 20 by default, from 0 to 90.\n" +
            "蓄电池电量低于这个百分比时，在面板中标为低优先级的电器会被关闭，电量回升到高出 10 个百分点后重新打开。0 为关闭此功能。默认 20，可设为 0 到 90。",
            new AcceptableValueRange<int>(0, SettingChange.ShedMost)));
        var fuelOrder = Config.Bind("Fuel", "Order", FuelOrder.Default,
            "The items used to refuel generators, by item id, first used first. Items not on the list are never used. Easiest to change in the panel.\n" +
            "用来给发电机添加燃料的物品（物品编号），排在前面的先用。不在列表中的物品永远不会被使用。在面板中修改最方便。");
        var rules = BindRules();

        if (isFirstStart) ImportOldSettings(log.Write);

        var harmony = new Harmony(Guid);
        var isDefenseManagerLoaded = IL2CPPChainloader.Instance.Plugins.ContainsKey(DefenseManagerGuid);
        var isGameRulesOld = OldMods.IsOld(InstalledVersion(OldMods.GameRulesGuid));
        var isMachineSpeedOld = OldMods.IsOld(InstalledVersion(OldMods.MachineSpeedGuid));
        var tables = new RuleTables(isGameRulesOld ? 1f : rules.BatteryMultiplier.Value, log.Write);
        var config = new PanelConfig(panelKey, autoRefuel, autoRefuelMinutes, shedBelow, fuelOrder);
        var store = new DeviceStore(Path.Combine(Paths.ConfigPath, DevicesFileName), log.Write);
        var session = new PanelSession(new GamePower(log.Write, isDefenseManagerLoaded), new WebPage(log.Write), config, store, log.Write);

        log.Write($"===== Power Manager {Version} started, panel key {panelKey.Value}, battery {(isGameRulesOld ? 1f : rules.BatteryMultiplier.Value):0.##}x the game's");
        WarnOldMods(isGameRulesOld, isMachineSpeedOld, log.Write);
        InputGate.Apply(harmony, log.Write);
        PageMessages.Apply(harmony, log.Write);

        if (!isGameRulesOld)
        {
            OptionalLive.Watch(() =>
            {
                var read = rules.Read();

                RulePatches.Use(harmony, read, log.Write);
                SolarDetour.Use(read, log.Write);
            }, rules.SolarMultiplier, rules.BadWeatherOutput, rules.DevicePowerUse, rules.PowerCutDelay, rules.NoPowerCut, rules.NoBlackoutPanic);
        }

        if (!isMachineSpeedOld)
        {
            OptionalLive.Watch(fuelBurn, value => RulePatches.UseBurn(harmony, Speed.From(value), log.Write));
            OptionalLive.Watch(efficiency, value => tables.UseEfficiency(Multiplier.From(value, 0.1f, 100f)));
        }

        PowerTicker.Tables = tables;
        PowerTicker.Session = session;
        PowerTicker.Log = log.Write;
        AddComponent<PowerTicker>();
    }

    private static string? InstalledVersion(string guid) =>
        IL2CPPChainloader.Instance.Plugins.TryGetValue(guid, out var info) ? info.Metadata.Version.ToString() : null;

    /// <summary>
    /// An old Game Rules or Machine Speed still applies the power settings this mod took over, so this mod leaves them to
    /// it (they would apply twice otherwise) and asks the player to update it.
    /// </summary>
    private static void WarnOldMods(bool isGameRulesOld, bool isMachineSpeedOld, Action<string> log)
    {
        var names = OldMods.Names(isGameRulesOld, isMachineSpeedOld);

        if (names.Length == 0) return;

        log($"{names} is older than 0.7.0 and still has the power settings, so this mod does not apply them until it is updated (an old version applies only the ones it has)");
        PowerTicker.Notice = () => Toasts.Show(new ToastRequest(Texts.Source.Pick(GameLanguage.IsChinese()), Texts.UpdateOldMods.Say(GameLanguage.IsChinese(), names)) { Kind = ToastKind.Warning });
    }

    /// <summary>A config file with no Generators section was never written by this mod, so its settings come from the old mods.</summary>
    private bool IsFirstStart(Action<string> log)
    {
        try
        {
            return !File.Exists(Config.ConfigFilePath) || !OldConfig.HasSection(File.ReadAllText(Config.ConfigFilePath), GeneratorsSection);
        }
        catch (Exception ex)
        {
            log($"could not read {Config.ConfigFilePath}, the old Game Rules and Machine Speed settings are not carried over: {ex.Message}");

            return false;
        }
    }

    /// <summary>
    /// Carries over the power settings a player changed in Game Rules and Machine Speed, which no longer have them. The
    /// old files are only read. A value this mod cannot take keeps its default and is logged.
    /// </summary>
    private void ImportOldSettings(Action<string> log)
    {
        var carried = OldConfig.Carried(ReadOld(OldConfig.GameRulesFile, log), ReadOld(OldConfig.MachineSpeedFile, log));

        foreach (var setting in carried)
        {
            try
            {
                if (!HasEntry(setting.Section, setting.Key))
                {
                    log($"no setting {setting.Section}/{setting.Key} to carry {setting.Value} into");

                    continue;
                }

                var entry = Config[new ConfigDefinition(setting.Section, setting.Key)];
                var before = entry.GetSerializedValue();

                // BepInEx logs a value it cannot read and keeps the old one, without throwing.
                entry.SetSerializedValue(setting.Value);

                var after = entry.GetSerializedValue();

                log(after != before || after == setting.Value
                    ? $"carried over {setting.Section}/{setting.Key} = {setting.Value}"
                    : $"could not read {setting.Section}/{setting.Key} = {setting.Value} (or it already was that), it is {after}");
            }
            catch (Exception ex)
            {
                log($"could not carry over {setting.Section}/{setting.Key} = {setting.Value}, it keeps its default: {ex.Message}");
            }
        }

        if (carried.Count > 0) Config.Save();
    }

    private bool HasEntry(string section, string key) => Config.ContainsKey(new ConfigDefinition(section, key));

    private static string? ReadOld(string fileName, Action<string> log)
    {
        var path = Path.Combine(Paths.ConfigPath, fileName);

        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            log($"could not read {path} to carry its power settings over: {ex.Message}");

            return null;
        }
    }

    private RuleEntries BindRules()
    {
        var game = PowerRules.Game;

        return new RuleEntries(
            Config.Bind("Power", "BatteryMultiplier", game.BatteryMultiplier,
                new ConfigDescription(
                    "Batteries hold this many times the game's power: 2 holds twice as much. 1 is the game's own. Takes effect at the next game start.\n" +
                    "蓄电池的容量为游戏原本的这么多倍：2 为两倍。1 为游戏原本的数值。下次启动游戏时生效。",
                    new AcceptableValueRange<float>(0.1f, 10f), "ModManager.Restart")),
            Config.Bind("Power", "DevicePowerUse", game.DevicePowerUse,
                new ConfigDescription(
                    "How much power switched-on devices use, times the game's use: 0.5 is half. Cooking and shredding jobs, the compressor, defense devices firing and the signal station use the game's amount. 1 is the game's own.\n" +
                    "开启的电器消耗的电量，为游戏原本的倍数：0.5 为一半。烹饪和粉碎工作、压缩机、防御设施开火和信号站的耗电与游戏原本相同。1 为游戏原本的数值。",
                    new AcceptableValueRange<float>(0f, 3f))),
            Config.Bind("Power", "PowerCutDelay", game.PowerCutDelay,
                new ConfigDescription(
                    "How many days later than in the game the city power is cut. 0 is the game's own. A cut that already happened stays.\n" +
                    "市电被切断（剧情事件「停电开始」）比游戏原本推迟的天数。0 为游戏原本的数值。已经停电的不会恢复。",
                    new AcceptableValueRange<int>(0, 100))),
            Config.Bind("Power", "NoPowerCut", game.NoPowerCut,
                "The city power is never cut, so the house keeps grid power all game. The story events that follow the cut never come, and the diesel generator cannot be started. A cut that already happened stays. Off: as in the game.\n" +
                "市电永远不会被切断（剧情事件「停电开始」不会出现），房子整局都有市电。停电之后的剧情事件不会出现，柴油发电机也无法启动。已经停电的不会恢复。关闭后与游戏原本相同。"),
            Config.Bind("Power", "NoBlackoutPanic", game.NoBlackoutPanic,
                "Power cuts at night no longer bring Blackout panic and its morale loss. A panic already running clears within ten in-game minutes. Off: as in the game.\n" +
                "夜间停电不会再带来「停电恐慌」及其心态下降。已有的停电恐慌会在游戏内十分钟内消除。关闭后与游戏原本相同。"),
            Config.Bind("Solar", "SolarMultiplier", game.SolarMultiplier,
                new ConfigDescription(
                    "Solar panels make this many times the game's power. 1 is the game's own.\n" +
                    "太阳能板的发电量为游戏原本的这么多倍。1 为游戏原本的数值。",
                    new AcceptableValueRange<float>(0.1f, 10f))),
            Config.Bind("Solar", "BadWeatherOutput", game.BadWeatherSolar,
                new ConfigDescription(
                    "In storms and heavy rain the game's solar panels make nothing. This gives them a share of a cloudy day's power instead: 0.5 is half, 1 is a full cloudy day. 0 is the game's own.\n" +
                    "在暴雨等恶劣天气中，游戏原本的太阳能板不发电。此项让它们发出阴天发电量的一部分：0.5 为一半，1 为与阴天相同。0 为游戏原本的数值。",
                    new AcceptableValueRange<float>(0f, 1f))));
    }

    private sealed record RuleEntries(
        ConfigEntry<float> BatteryMultiplier,
        ConfigEntry<float> DevicePowerUse,
        ConfigEntry<int> PowerCutDelay,
        ConfigEntry<bool> NoPowerCut,
        ConfigEntry<bool> NoBlackoutPanic,
        ConfigEntry<float> SolarMultiplier,
        ConfigEntry<float> BadWeatherOutput)
    {
        public PowerRules Read() => new(SolarMultiplier.Value, BadWeatherOutput.Value, BatteryMultiplier.Value, DevicePowerUse.Value, PowerCutDelay.Value, NoPowerCut.Value, NoBlackoutPanic.Value);
    }
}
