using System.Collections.Generic;
using System.Globalization;

namespace PowerManagerMod.Core;

/// <summary>One piece of on-screen text in the two languages the game ships. {0}, {1} are filled in by Say.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Say(bool isChinese, params object[] args) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), args);
}

/// <summary>A phrase about a count: English says "1 generator" and "2 generators", Chinese has one form for both. {0} is the count.</summary>
public readonly record struct Counted(Phrase One, Phrase Many)
{
    public string Say(bool isChinese, int count, params object[] more)
    {
        var args = new object[more.Length + 1];

        args[0] = count;
        more.CopyTo(args, 1);

        return (count == 1 ? One : Many).Say(isChinese, args);
    }
}

/// <summary>The words the mod puts on screen: press results, toasts and the panel's own labels.</summary>
public static class Texts
{
    private const string Log = "BepInEx/power-manager.log";

    // Generators.
    public static readonly Phrase Started = new("Started {0}.", "已启动{0}。");
    public static readonly Phrase Stopped = new("Stopped {0}.", "已关闭{0}。");
    public static readonly Phrase StoppedAutoOff = new("Stopped {0}. Stopping a generator also turns its auto-start off.", "已关闭{0}。关闭发电机时，它的自动启动也会关闭。");
    public static readonly Phrase NoFuel = new("{0} has no fuel.", "{0}没有燃料。");
    public static readonly Phrase Locked = new("The game is holding the power switches right now.", "游戏此刻锁定了电源开关。");
    public static readonly Phrase GeneratorGone = new("That generator is gone.", "这台发电机已经不在了。");
    public static readonly Phrase AutoOn = new("Auto-start on for {0}: it starts below {1}% battery.", "{0}的自动启动已开启：电量低于 {1}% 时启动。");
    public static readonly Phrase AutoOff = new("Auto-start off for {0}.", "{0}的自动启动已关闭。");
    public static readonly Phrase NothingToStart = new("No generator with fuel is waiting to start.", "没有可启动的有燃料发电机。");
    public static readonly Phrase NothingToStop = new("No generator is running.", "没有正在运行的发电机。");
    public static readonly Phrase NoGenerators = new("No fuel generators at home.", "家里没有燃料发电机。");
    public static readonly Counted StartingAll = new(new("Starting 1 generator.", "正在启动 {0} 台发电机。"), new("Starting {0} generators.", "正在启动 {0} 台发电机。"));
    public static readonly Counted StoppingAll = new(new("Stopping 1 generator.", "正在关闭 {0} 台发电机。"), new("Stopping {0} generators.", "正在关闭 {0} 台发电机。"));
    public static readonly Counted AutoAllOn = new(new("Auto-start on for 1 generator.", "已为 {0} 台发电机开启自动启动。"), new("Auto-start on for {0} generators.", "已为 {0} 台发电机开启自动启动。"));
    public static readonly Counted AutoAllOff = new(new("Auto-start off for 1 generator.", "已为 {0} 台发电机关闭自动启动。"), new("Auto-start off for {0} generators.", "已为 {0} 台发电机关闭自动启动。"));

    // Fuel.
    public static readonly Phrase AlreadyFull = new("{0} is already full.", "{0}已经加满。");
    public static readonly Phrase AllFull = new("Every generator is full.", "所有发电机都已加满。");
    public static readonly Phrase NoFuelInStorage = new("No fuel from your fuel list in home storage.", "家中储物里没有燃料列表中的燃料。");

    /// <summary>{0} is how many, {1} the generator.</summary>
    public static readonly Counted Refuelled = new(new("Added 1 fuel to {1}.", "已为{1}添加 {0} 个燃料。"), new("Added {0} fuel to {1}.", "已为{1}添加 {0} 个燃料。"));

    public static readonly Counted RefuelledRanOut = new(
        new("Added 1 fuel to {1}, then ran out of fuel from your list.", "已为{1}添加 {0} 个燃料，之后燃料列表中的燃料用完了。"),
        new("Added {0} fuel to {1}, then ran out of fuel from your list.", "已为{1}添加 {0} 个燃料，之后燃料列表中的燃料用完了。"));

    public static readonly Counted AutoRefuelled = new(new("Auto-refuel: added 1 fuel to {1}.", "自动加燃料：已为{1}添加 {0} 个燃料。"), new("Auto-refuel: added {0} fuel to {1}.", "自动加燃料：已为{1}添加 {0} 个燃料。"));
    public static readonly Phrase AutoNoFuel = new("Auto-refuel: {0} is low, and there is no fuel from your fuel list in home storage.", "自动加燃料：{0}燃料不足，家中储物里没有燃料列表中的燃料。");
    public static readonly Phrase FuelListSaved = new("Fuel list saved.", "燃料列表已保存。");

    // Devices.
    public static readonly Phrase SwitchedOn = new("{0} switched on.", "{0}已开启。");
    public static readonly Phrase SwitchedOff = new("{0} switched off.", "{0}已关闭。");
    public static readonly Phrase NotSwitched = new("The game kept {0} as it was.", "游戏没有切换{0}。");
    public static readonly Phrase DeviceGone = new("That device is gone.", "这个设备已经不在了。");
    public static readonly Phrase Managed = new("Defense Manager runs the power of {0}.", "{0}的供电由 Defense Manager 控制。");
    public static readonly Phrase MarkedLow = new("{0} is low priority now: it is switched off first when the battery runs low.", "{0}现在是低优先级：电量不足时最先关闭。");
    public static readonly Phrase MarkedNormal = new("{0} is normal priority now.", "{0}现在是普通优先级。");
    public static readonly Counted Shedding = new(new("Battery low: switching off 1 low-priority device.", "电量低：正在关闭 {0} 个低优先级设备。"), new("Battery low: switching off {0} low-priority devices.", "电量低：正在关闭 {0} 个低优先级设备。"));
    public static readonly Counted Restoring = new(new("Switching 1 low-priority device back on.", "正在重新开启 {0} 个低优先级设备。"), new("Switching {0} low-priority devices back on.", "正在重新开启 {0} 个低优先级设备。"));
    public static readonly Phrase NoSave = new("Device priorities are kept per save, and this game has no save yet. Save the game first.", "设备优先级按存档保存，而这局游戏还没有存档。请先保存游戏。");
    public static readonly Phrase Unreadable = new("The devices file could not be read, so nothing was changed. See " + Log + ".", "无法读取设备文件，因此没有做任何更改。详见 " + Log + "。");
    public static readonly Phrase NotSaved = new("It could not be written to the devices file, so it lasts until the game is closed.", "设置无法写入设备文件，只在关闭游戏前有效。");

    // Anywhere.
    public static readonly Phrase NoGame = new("Load a game first.", "请先载入游戏。");

    /// <summary>Who a toast is from, on its first line.</summary>
    public static readonly Phrase Source = new("Power Manager", "电力管理");

    public static readonly Phrase Failed = new("Power Manager failed, see " + Log + ".", "Power Manager 出错，详见 " + Log + "。");
    public static readonly Phrase Saved = new("Setting saved.", "设置已保存。");
    public static readonly Phrase SavedAtRestart = new("Setting saved. It takes effect at the next game start.", "设置已保存，下次启动游戏时生效。");

    /// <summary>The words page.js draws the panel and the HUD button's tip with, by the key it looks them up by.</summary>
    public static readonly IReadOnlyList<(string Key, Phrase Phrase)> Page = new (string, Phrase)[]
    {
        ("title", new("POWER", "电力")),
        ("tip", new("Generators, fuel and home power", "发电机、燃料和家中供电")),
        ("tabPower", new("Power", "供电")),
        ("tabFuel", new("Fuel", "燃料")),
        ("tabSettings", new("Settings", "设置")),
        ("battery", new("Battery", "蓄电池")),
        ("stored", new("{0} of {1} stored", "已储存 {0} / {1}")),
        ("genCons", new("Making {0}, using {1} every 10 minutes", "每 10 分钟发电 {0}，用电 {1}")),
        ("fcCity", new("City power is on, the battery is not in use.", "城市供电正常，蓄电池未在使用。")),
        ("fcNoBattery", new("No battery built.", "还没有建造蓄电池。")),
        ("fcSteady", new("Holding steady.", "电量保持不变。")),
        ("fcDraining", new("Empty in {0}", "{0}后耗尽")),
        ("fcCharging", new("Full in {0}", "{0}后充满")),
        ("fcEmpty", new("Empty.", "已耗尽。")),
        ("fcFull", new("Full.", "已充满。")),
        ("locked", new("The game is holding the power switches right now.", "游戏此刻锁定了电源开关。")),
        ("hours", new("{0} h {1} min", "{0} 小时 {1} 分钟")),
        ("minutes", new("{0} min", "{0} 分钟")),
        ("days", new("{0} d {1} h", "{0} 天 {1} 小时")),
        ("generators", new("Generators", "发电机")),
        ("noGenerators", new("No fuel generators at home yet.", "家里还没有燃料发电机。")),
        ("running", new("Running", "运行中")),
        ("stopped", new("Stopped", "已停止")),
        ("fuelLeft", new("Fuel: {0}", "燃料：{0}")),
        ("noFuel", new("No fuel", "没有燃料")),
        ("slots", new("{0} of {1} slots", "{0} / {1} 格")),
        ("start", new("Start", "启动")),
        ("stop", new("Stop", "关闭")),
        ("refuel", new("Refuel", "加燃料")),
        ("refuelling", new("Refuelling", "正在加燃料")),
        ("auto", new("Auto-start", "自动启动")),
        ("autoBelow", new("below {0}%", "低于 {0}%")),
        ("startAll", new("Start all", "全部启动")),
        ("stopAll", new("Stop all", "全部关闭")),
        ("refuelAll", new("Refuel all", "全部加燃料")),
        ("autoAllOn", new("Auto-start all", "全部自动启动")),
        ("autoAllOff", new("Auto-start none", "全部取消自动启动")),
        ("devices", new("Devices", "用电设备")),
        ("noDevices", new("No powered devices at home yet.", "家里还没有用电设备。")),
        ("perTick", new("{0} per 10 min", "每 10 分钟 {0}")),
        ("on", new("On", "开")),
        ("off", new("Off", "关")),
        ("low", new("Low priority", "低优先级")),
        ("shed", new("Switched off to save the battery", "为节省电量已关闭")),
        ("managed", new("Run by Defense Manager", "由 Defense Manager 控制")),
        ("shedNote", new("Low-priority devices switch off below {0}% battery during a power cut, and back on at {1}%.", "停电期间电量低于 {0}% 时关闭低优先级设备，回到 {1}% 时重新开启。")),
        ("shedOffNote", new("Load shedding is off. Turn it on in Settings.", "负载削减已关闭。可在设置中开启。")),
        ("fuelOrder", new("Fuel order", "燃料顺序")),
        ("fuelNote", new("Refuelling takes fuel from home storage in this order, top first. Items not on the list are never used.", "加燃料时按此顺序从家中储物取用，从上往下。不在列表中的物品不会被使用。")),
        ("inStorage", new("{0} in storage", "储物中 {0} 个")),
        ("up", new("Up", "上移")),
        ("down", new("Down", "下移")),
        ("removeFuel", new("Remove", "移除")),
        ("addFuel", new("Add", "添加")),
        ("otherFuel", new("Other fuel in storage", "储物中的其他燃料")),
        ("noOtherFuel", new("No other burnable items in home storage.", "家中储物里没有其他可燃物品。")),
        ("emptyOrder", new("The fuel list is empty, so nothing is used for refuelling.", "燃料列表为空，加燃料时不会使用任何物品。")),
        ("setControls", new("Controls", "控制")),
        ("setKey", new("Open the panel", "打开面板")),
        ("setKeyDesc", new("Key that opens and closes this window.", "打开和关闭此窗口的按键。")),
        ("keyNone", new("None", "无")),
        ("setGenerators", new("Generators", "发电机")),
        ("setAutoRefuel", new("Auto-refuel", "自动加燃料")),
        ("setAutoRefuelDesc", new("Fill a running generator, or one set to auto-start, from home storage when its fuel runs low.", "运行中或设为自动启动的发电机燃料不足时，从家中储物自动加满。")),
        ("setAutoMinutes", new("Refuel below", "低于此时间时加燃料")),
        ("setAutoMinutesDesc", new("Auto-refuel when a generator has less than this many game minutes of fuel.", "发电机剩余燃料少于这么多游戏分钟时自动加燃料。")),
        ("setShedding", new("Load shedding", "负载削减")),
        ("setShedBelow", new("Shed below", "低于此电量时削减")),
        ("setShedBelowDesc", new("During a power cut, switch off low-priority devices below this battery level. 0 turns it off.", "停电期间电量低于此值时关闭低优先级设备。0 为关闭。")),
        ("percent", new("{0}%", "{0}%")),
        ("keyClose", new("{0} or Esc to close", "按 {0} 或 Esc 关闭")),
        ("escClose", new("Esc to close", "按 Esc 关闭")),
        ("noGame", new("Load a game first.", "请先载入游戏。")),
    };

    public static IReadOnlyList<Counted> Counts { get; } = new[] { StartingAll, StoppingAll, AutoAllOn, AutoAllOff, Refuelled, RefuelledRanOut, AutoRefuelled, Shedding, Restoring };

    public static IReadOnlyList<Phrase> All { get; } = Collect();

    private static List<Phrase> Collect()
    {
        var all = new List<Phrase>
        {
            Started, Stopped, StoppedAutoOff, NoFuel, Locked, GeneratorGone, AutoOn, AutoOff, NothingToStart, NothingToStop, NoGenerators,
            AlreadyFull, AllFull, NoFuelInStorage, AutoNoFuel, FuelListSaved,
            SwitchedOn, SwitchedOff, NotSwitched, DeviceGone, Managed, MarkedLow, MarkedNormal, NoSave, Unreadable, NotSaved,
            NoGame, Source, Failed, Saved, SavedAtRestart,
        };

        foreach (var counted in Counts)
        {
            all.Add(counted.One);
            all.Add(counted.Many);
        }

        foreach (var (_, phrase) in Page)
        {
            if (!all.Contains(phrase)) all.Add(phrase);
        }

        return all;
    }
}
