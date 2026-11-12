using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PowerManagerMod.Adapters;
using PowerManagerMod.Core;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using UnityEngine;

namespace PowerManagerMod;

[BepInPlugin(Guid, "Power Manager", Version)]
[BepInDependency(ToolkitGuid, ToolkitVersion)]
// Loaded after these when they are installed, so their versions can be read (see OldMods).
[BepInDependency(OldMods.GameRulesGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(OldMods.MachineSpeedGuid, BepInDependency.DependencyFlags.SoftDependency)]
// Loaded after Wind Power when it is installed, so its turbine multipliers can be found (see WindLink).
[BepInDependency(WindLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.powermanager";
    private const string Version = "0.5.2";

    /// <summary>The Wind setting, as the turbine multiplier Wind Power reads once a second.</summary>
    private static volatile float _windMultiplier = 1f;

    /// <summary>The SmogOutput setting, as the share of a Clear day's wind Wind Power gives Heavy Smog.</summary>
    private static volatile float _smogShare;

    /// <summary>Exiled's Toolkit: its shared helpers, toasts, live settings, UI size and typing check all come from it.</summary>
    private const string ToolkitGuid = "com.dev.survivallog.exiledtoolkit";
    private const string ToolkitVersion = ">=0.19.0";

    private const string LogFileName = "power-manager.log";

    /// <summary>The name the page script hands its panels to the Toolkit's UI size with.</summary>
    private const string UiSizeName = "PowerManager";

    private const string DefenseManagerGuid = "com.dev.survivallog.defensemanager";
    private const string DevicesFileName = Guid + ".devices.txt";
    private const string GeneratorsSection = "Generators";

    // With Exiled's Toolkit 0.17.0 or newer, a setting with this tag can be set per world (Customizable Difficulty
    // picks it for a new world). Every tagged setting must be watched below, or the Toolkit leaves it out.
    private const string WorldRule = "ModManager.WorldRule";

    // A setting the mod reads each time it uses it, so the Mod Manager shows it applies at once without a watch.
    private const string LiveTag = "ModManager.Live";

    private const string RateRange = "From 0.1 to 100: 0 or less counts as 1, between 0 and 0.1 counts as 0.1, more than 100 counts as 100.";
    private const string RateRangeChinese = "范围 0.1 到 100：0 或更小按 1 计算，0 到 0.1 之间按 0.1 计算，大于 100 按 100 计算。";

    public override void Load()
    {
        var log = new ModLog(Log, LogFileName);
        var isFirstStart = IsFirstStart(log.Write);

        ModUiSize.Bind(Config, UiSizeName);

        var panelKey = Config.Bind("Controls", "PanelKey", Enum.Parse<KeyCode>(Hotkey.DefaultKey), Live(
            "Key that opens and closes the Power Manager panel. Unbound by default: the lightning button in the bottom-left corner of the screen opens the panel. Set a key here to open it from the keyboard too.\n" +
            "打开和关闭 Power Manager 面板的按键。默认不绑定：屏幕左下角的闪电按钮可以打开面板。在这里设置按键后，也可以用键盘打开。"));
        var autoRefuel = Config.Bind(GeneratorsSection, "AutoRefuel", false, Live(
            "Refuels a running generator, or one set to auto-start, from home storage when it has less than AutoRefuelMinutes of fuel left. It uses the fuel order. Off by default.\n" +
            "运行中或设为自动启动的发电机剩余燃料少于 AutoRefuelMinutes 时，自动按燃料顺序从家里的储物中添加燃料。默认关闭。"));
        var autoRefuelMinutes = Config.Bind(GeneratorsSection, "AutoRefuelMinutes", 60, new ConfigDescription(
            "With AutoRefuel on, a generator with less than this many in-game minutes of fuel left is refuelled. 60 by default, from 10 to 1440.\n" +
            "开启 AutoRefuel 时，剩余燃料少于这么多游戏分钟的发电机会被添加燃料。默认 60，可设为 10 到 1440。",
            new AcceptableValueRange<int>(SettingChange.MinutesLeast, SettingChange.MinutesMost), LiveTag));
        var efficiency = Config.Bind(GeneratorsSection, "GeneratorEfficiency", 1f, Rule(
            $"How much power fuel generators make from the same fuel: 2 is twice the power from each unit of fuel, 0.5 is half. How fast they burn fuel is FuelBurn. {RateRange}\n" +
            $"燃油发电机消耗相同燃料时的发电量：2 为每份燃料发出两倍电力，0.5 为一半。燃料消耗速度由 FuelBurn 设置。{RateRangeChinese}"));
        var fuelBurn = Config.Bind(GeneratorsSection, "FuelBurn", 1f, Rule(
            "How fast fuel generators burn fuel: 2 burns it twice as fast, 0.5 makes it last twice as long. From 0.25 to 100000. 0 or less counts as 1.\n" +
            "燃油发电机消耗燃料的速度：2 为两倍消耗，0.5 让燃料多用一倍时间。范围 0.25 到 100000，0 或更小按 1 计算。"));
        var shedBelow = Config.Bind("Shedding", "ShedBelow", 20, new ConfigDescription(
            "During a power cut, devices marked low priority in the panel switch off when the battery falls below this percent, and back on once it is 10 points above. 0 turns this off. 20 by default, from 0 to 90.\n" +
            "停电期间，蓄电池电量低于这个百分比时，在面板中标为低优先级的电器会被关闭，电量回升到高出 10 个百分点后重新打开。0 为关闭此功能。默认 20，可设为 0 到 90。",
            new AcceptableValueRange<int>(0, SettingChange.ShedMost), LiveTag));
        var fuelOrder = Config.Bind("Fuel", "Order", FuelOrder.Default, Live(
            "The items used to refuel generators, by item id, first used first. Items not on the list are never used. Easiest to change in the panel.\n" +
            "用来给发电机添加燃料的物品（物品编号），排在前面的先用。不在列表中的物品永远不会被使用。在面板中修改最方便。"));
        var rules = BindRules();

        if (isFirstStart) ImportOldSettings(log.Write);

        var hooks = new PatchKit(new Harmony(Guid), log.Write);
        var isDefenseManagerLoaded = IL2CPPChainloader.Instance.Plugins.ContainsKey(DefenseManagerGuid);
        var isGameRulesOld = OldMods.IsOld(InstalledVersion(OldMods.GameRulesGuid));
        var isMachineSpeedOld = OldMods.IsOld(InstalledVersion(OldMods.MachineSpeedGuid));
        var tables = new RuleTables(isGameRulesOld ? () => 1f : () => rules.BatteryMultiplier.Value, log.Write);
        var config = new PanelConfig(panelKey, autoRefuel, autoRefuelMinutes, shedBelow, fuelOrder);
        var store = new DeviceStore(Path.Combine(Paths.ConfigPath, DevicesFileName), log.Write);
        var session = new PanelSession(new GamePower(log.Write, isDefenseManagerLoaded), new PowerPage(log.Write), config, store, log.Write);

        log.Write($"===== Power Manager {Version} started, panel key {panelKey.Value}, battery {(isGameRulesOld ? 1f : rules.BatteryMultiplier.Value):0.##}x the game's");
        WarnOldMods(isGameRulesOld, isMachineSpeedOld, log.Write);

        if (!isGameRulesOld)
        {
            LiveSettings.Watch(() =>
            {
                var read = rules.Read();

                RulePatches.Use(hooks, read, log.Write);
                SolarDetour.Use(read, log.Write);
            }, rules.SolarMultiplier, rules.BadWeatherOutput, rules.DevicePowerUse, rules.PowerCutDelay, rules.NoPowerCut, rules.NoBlackoutPanic);
            LiveSettings.Watch(rules.BatteryMultiplier, _ => tables.CheckSoon());
            RulePatches.UseTables(hooks, tables, log.Write);
        }

        if (!isMachineSpeedOld)
        {
            LiveSettings.Watch(fuelBurn, value => RulePatches.UseBurn(hooks, Speed.From(value), log.Write));
            LiveSettings.Watch(efficiency, value => tables.UseEfficiency(Multiplier.From(value, 0.1f, 100f)));
        }

        BindWind(log.Write);

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
                    "Batteries hold this many times their normal power: 2 holds twice as much. 1 is the default. Lowering it drops any stored power above the new size.\n" +
                    "蓄电池的容量为正常容量的这么多倍：2 为两倍。1 为默认值。调低后，超出新容量的储存电量会丢失。",
                    new AcceptableValueRange<float>(0.1f, 10f), WorldRule)),
            Config.Bind("Power", "DevicePowerUse", game.DevicePowerUse,
                new ConfigDescription(
                    "How much power switched-on devices use, as a multiple of their normal use: 0.5 is half. Cooking and shredding jobs, the compressor, defense devices firing and the signal station are not changed. 1 is the default.\n" +
                    "开启的电器消耗的电量，为正常耗电的倍数：0.5 为一半。烹饪和粉碎工作、压缩机、防御设施开火和信号站的耗电不变。1 为默认值。",
                    new AcceptableValueRange<float>(0f, 3f), WorldRule)),
            Config.Bind("Power", "PowerCutDelay", game.PowerCutDelay,
                new ConfigDescription(
                    "How many days later than normal the city power is cut. 0 is the default. A cut that already happened stays.\n" +
                    "市电被切断（剧情事件「停电开始」）比正常推迟的天数。0 为默认值。已经停电的不会恢复。",
                    new AcceptableValueRange<int>(0, 100), WorldRule)),
            Config.Bind("Power", "NoPowerCut", game.NoPowerCut, Rule(
                "The city power is never cut, so the house keeps grid power all game. The story events that follow the cut never come, and the diesel generator cannot be started. A cut that already happened stays. Off by default.\n" +
                "市电永远不会被切断（剧情事件「停电开始」不会出现），房子整局都有市电。停电之后的剧情事件不会出现，柴油发电机也无法启动。已经停电的不会恢复。默认关闭。")),
            Config.Bind("Power", "NoBlackoutPanic", game.NoBlackoutPanic, Rule(
                "Power cuts at night no longer bring Blackout panic and its morale loss. A panic already running clears within ten in-game minutes. Off by default.\n" +
                "夜间停电不会再带来「停电恐慌」及其心态下降。已有的停电恐慌会在游戏内十分钟内消除。默认关闭。")),
            Config.Bind("Solar", "SolarMultiplier", game.SolarMultiplier,
                new ConfigDescription(
                    "Solar panels make this many times their normal power. 1 is the default.\n" +
                    "太阳能板的发电量为正常发电量的这么多倍。1 为默认值。",
                    new AcceptableValueRange<float>(0.1f, 10f), WorldRule)),
            Config.Bind("Solar", "BadWeatherOutput", game.BadWeatherSolar,
                new ConfigDescription(
                    "In storms and heavy rain, solar panels make nothing. This gives them a share of a cloudy day's power instead: 0.5 is half, 1 is a full cloudy day. 0 is the default.\n" +
                    "在暴雨等恶劣天气中，太阳能板不发电。此项让它们发出阴天发电量的一部分：0.5 为一半，1 为与阴天相同。0 为默认值。",
                    new AcceptableValueRange<float>(0f, 1f), WorldRule)));
    }

    /// <summary>
    /// The Wind settings, only with Wind Power installed: WindMultiplier (0.9.3 or newer) multiplies every turbine's
    /// power, like SolarMultiplier does for the solar panels, and SmogOutput (0.9.4 or newer) gives Heavy Smog some
    /// wind, like BadWeatherOutput gives the panels some light in storms.
    /// </summary>
    private void BindWind(Action<string> log)
    {
        if (!IL2CPPChainloader.Instance.Plugins.ContainsKey(WindLink.Guid)) return;

        // A failure here costs only the Wind setting, never the rest of the mod.
        try
        {
            if (!WindLink.Add(AppDomain.CurrentDomain.GetAssemblies(), "Power Manager", () => _windMultiplier))
            {
                log("Wind Power is older than 0.9.3, so there is no Wind setting until it is updated");

                return;
            }
        }
        catch (Exception ex)
        {
            log("could not reach Wind Power, so there is no Wind setting: " + ex.Message);

            return;
        }

        var wind = Config.Bind("Wind", "WindMultiplier", 1f, new ConfigDescription(
            "Wind Power's turbines make this many times their normal power. 1 leaves them as they are. Shown only with Wind Power installed.\n" +
            "Wind Power 的风力发电机的发电量为原本的这么多倍。1 为原本的数值。只在安装了 Wind Power 时显示。",
            new AcceptableValueRange<float>(WindLink.Least, WindLink.Most), WorldRule));

        LiveSettings.Watch(wind, value => _windMultiplier = WindLink.Multiplier(value));
        log($"Wind Power found, turbines make {_windMultiplier:0.##}x their power");

        try
        {
            if (!WindLink.AddCalm(AppDomain.CurrentDomain.GetAssemblies(), "Power Manager", () => _smogShare))
            {
                log("Wind Power is older than 0.9.4, so there is no SmogOutput setting until it is updated");

                return;
            }
        }
        catch (Exception ex)
        {
            log("could not reach Wind Power, so there is no SmogOutput setting: " + ex.Message);

            return;
        }

        var smog = Config.Bind("Wind", "SmogOutput", 0f, new ConfigDescription(
            "In Heavy Smog the air is still and Wind Power's turbines make nothing. This gives them a share of a Clear day's wind instead: 0.5 is half, 1 is a full Clear day. 0 leaves them still. Once Wind Power's HeavySmog setting is above 0, that wind is used instead. Shown only with Wind Power installed.\n" +
            "「大雾霾天」没有风，Wind Power 的风力发电机不发电。此项让它们获得「晴天」风力的一部分：0.5 为一半，1 为与「晴天」相同。0 为不转。Wind Power 的 HeavySmog 设置大于 0 时，改用那个风力。只在安装了 Wind Power 时显示。",
            new AcceptableValueRange<float>(0f, 1f), WorldRule));

        LiveSettings.Watch(smog, value => _smogShare = WindLink.CalmShare(value));
        log($"Heavy Smog gets {_smogShare:0.##} of a Clear day's wind");
    }

    private static ConfigDescription Rule(string description) => new(description, null, WorldRule);

    private static ConfigDescription Live(string description) => new(description, null, LiveTag);

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
