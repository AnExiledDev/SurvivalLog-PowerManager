using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>The panel's buttons and the automatic passes, against a home that answers as the game does.</summary>
public class PowerRunTests
{
    private static readonly DeviceKey LampKey = new(1, 0, 0, 0);
    private static readonly DeviceKey HeaterKey = new(2, 0, 0, 0);

    private static Generator Gen(long id, bool isOn = false, int slots = 1, int max = 4, float seconds = 600f, bool auto = false) =>
        new(id, "Gen " + id, isOn, slots, max, seconds, auto, 0.3f);

    private static (FakePowerGame Game, PowerRun Run) Home(params Generator[] generators)
    {
        var game = new FakePowerGame();

        game.Generators.AddRange(generators);

        return (game, new PowerRun(game) { Order = FuelOrder.Parse("42001,20101,40000") });
    }

    [Fact]
    public void Start_all_starts_only_stopped_generators_that_have_fuel_and_says_how_many()
    {
        var (game, run) = Home(Gen(1), Gen(2, isOn: true), Gen(3, slots: 0), Gen(4));

        Assert.Equal("Starting 2 generators.", run.StartAll(game.Read(), isChinese: false).Text);
        Assert.Empty(game.RunAll(run));
        Assert.Equal(new[] { "toggle 1", "toggle 4" }, game.Calls);
    }

    [Fact]
    public void Stop_all_with_nothing_running_says_so_and_calls_nothing()
    {
        var (game, run) = Home(Gen(1));

        Assert.Equal("No generator is running.", run.StopAll(game.Read(), isChinese: false).Text);
        Assert.False(run.HasWork);
    }

    [Fact]
    public void Starting_a_generator_already_running_does_not_toggle_it_off()
    {
        var (game, run) = Home(Gen(1, isOn: true));

        run.Start(1);
        game.RunAll(run);

        Assert.Empty(game.Calls);
        Assert.True(game.Generators[0].IsOn);
    }

    [Fact]
    public void Nothing_is_pressed_while_the_game_holds_the_switches()
    {
        var (game, run) = Home(Gen(1));

        game.Battery = game.Battery with { IsLocked = true };
        run.Start(1);

        Assert.Equal(new[] { "The game is holding the power switches right now." }, game.RunAll(run));
        Assert.Empty(game.Calls);
    }

    [Fact]
    public void Refuel_follows_the_fuel_list_and_never_touches_an_item_off_it()
    {
        var (game, run) = Home(Gen(1, slots: 1, max: 4));

        game.Storage.Add(new FuelStack(500, 40000, 5));
        game.Storage.Add(new FuelStack(501, 99999, 5));
        game.Storage.Add(new FuelStack(502, 42001, 2));

        run.Refuel(1);

        Assert.Equal(new[] { "Added 3 fuel to Gen 1." }, game.RunAll(run));
        Assert.Equal(new[] { "fuel 1 42001", "fuel 1 42001", "fuel 1 40000" }, game.Calls);
        Assert.Contains(new FuelStack(501, 99999, 5), game.Storage);
        Assert.False(run.IsRefuelling(1));
    }

    [Fact]
    public void A_stack_the_game_refuses_is_skipped_for_the_next_on_the_list()
    {
        var (game, run) = Home(Gen(1, slots: 3, max: 4));

        game.Storage.Add(new FuelStack(500, 42001, 5));
        game.Storage.Add(new FuelStack(501, 20101, 5));
        game.Unburnable.Add(500);

        run.Refuel(1);

        Assert.Equal(new[] { "Added 1 fuel to Gen 1." }, game.RunAll(run));
        Assert.Equal(new[] { "fuel 1 20101" }, game.Calls);
    }

    [Fact]
    public void Refuelling_that_runs_out_says_what_went_in_and_that_the_list_ran_out()
    {
        var (game, run) = Home(Gen(1, slots: 0, max: 4));

        game.Storage.Add(new FuelStack(500, 42001, 1));
        run.Refuel(1);

        Assert.Equal(new[] { "Added 1 fuel to Gen 1, then ran out of fuel from your list." }, game.RunAll(run));
    }

    [Fact]
    public void Refuel_all_skips_full_generators_and_says_so_when_all_are_full()
    {
        var (game, run) = Home(Gen(1, slots: 4, max: 4), Gen(2, slots: 4, max: 4));

        Assert.Equal("Every generator is full.", run.RefuelAll(game.Read(), isChinese: false)?.Text);
        Assert.False(run.HasWork);
    }

    [Fact]
    public void Auto_refuel_tops_up_a_low_running_generator_and_reports_it()
    {
        var (game, run) = Home(Gen(1, isOn: true, slots: 2, max: 3, seconds: 600f), Gen(2, slots: 1, seconds: 60f));

        game.Storage.Add(new FuelStack(500, 20101, 9));

        run.AutoPass(game.Read(), shedBelow: 0, autoRefuelMinutes: 60, now: 0f, isChinese: false);

        Assert.Equal(new[] { "Auto-refuel: added 1 fuel to Gen 1." }, game.RunAll(run));
        Assert.Equal(new[] { "fuel 1 20101" }, game.Calls);
    }

    [Fact]
    public void Auto_refuel_with_no_fuel_warns_once_and_waits_before_trying_again()
    {
        var (game, run) = Home(Gen(1, isOn: true, seconds: 60f));

        run.AutoPass(game.Read(), 0, 60, now: 0f, isChinese: false);
        Assert.Equal(new[] { "Auto-refuel: Gen 1 is low, and there is no fuel from your fuel list in home storage." }, game.RunAll(run, now: 0f));

        run.AutoPass(game.Read(), 0, 60, now: 10f, isChinese: false);
        Assert.False(run.HasWork);

        run.AutoPass(game.Read(), 0, 60, now: PowerRun.StarvedRetrySeconds + 1f, isChinese: false);
        Assert.Empty(game.RunAll(run, now: PowerRun.StarvedRetrySeconds + 1f));
    }

    [Fact]
    public void Load_shedding_switches_low_devices_off_and_gives_back_only_those_it_switched_off()
    {
        var (game, run) = Home();

        game.Devices.Add(new Device(10, LampKey, "Lamp", true, 5f));
        game.Devices.Add(new Device(11, HeaterKey, "Heater", false, 9f));
        run.Book.Low.Add(LampKey);
        run.Book.Low.Add(HeaterKey);
        game.Battery = game.Battery with { Stored = 100f };

        Assert.Equal("Battery low: switching off 1 low-priority device.", run.AutoPass(game.Read(), 20, 0, 0f, false)?.Text);
        game.RunAll(run);
        Assert.False(game.Devices[0].IsOn);
        Assert.Contains(LampKey, run.Book.Shed);
        Assert.True(run.Book.Changed);

        game.Battery = game.Battery with { Stored = 300f };

        Assert.Equal("Switching 1 low-priority device back on.", run.AutoPass(game.Read(), 20, 0, 0f, false)?.Text);
        game.RunAll(run);
        Assert.True(game.Devices[0].IsOn);
        Assert.False(game.Devices[1].IsOn);
        Assert.Empty(run.Book.Shed);
    }

    [Fact]
    public void A_device_the_game_would_not_switch_is_not_counted_as_shed()
    {
        var (game, run) = Home();

        game.Devices.Add(new Device(10, LampKey, "Lamp", true, 5f));
        game.Stuck.Add(10);
        run.Book.Low.Add(LampKey);
        game.Battery = game.Battery with { Stored = 100f };

        run.AutoPass(game.Read(), 20, 0, 0f, false);
        game.RunAll(run);

        Assert.Empty(run.Book.Shed);
    }

    [Fact]
    public void A_player_switch_tells_them_when_the_game_kept_the_device_as_it_was()
    {
        var (game, run) = Home();

        game.Devices.Add(new Device(10, LampKey, "Lamp", true, 5f));
        game.Stuck.Add(10);
        run.Switch(10, false);

        Assert.Equal(new[] { "The game kept Lamp as it was." }, game.RunAll(run));
    }

    [Fact]
    public void A_defense_manager_device_cannot_be_marked_or_switched()
    {
        var (game, run) = Home();

        game.Devices.Add(new Device(10, LampKey, "Turret", true, 5f, IsManaged: true));

        Assert.Equal("Defense Manager runs the power of Turret.", run.MarkLow(game.Read(), 10, true, false).Text);
        run.Switch(10, false);
        game.RunAll(run);

        Assert.Empty(run.Book.Low);
        Assert.Empty(game.Calls);
    }

    [Fact]
    public void Auto_start_at_a_new_level_is_sent_with_that_level()
    {
        var (game, run) = Home(Gen(1));

        run.AutoStart(1, true, 0.5f);

        Assert.Equal(new[] { "Auto-start on for Gen 1: it starts below 50% battery." }, game.RunAll(run));
        Assert.Equal(new[] { "auto 1 True 0.5" }, game.Calls);
    }

    [Fact]
    public void Clear_drops_the_old_games_work()
    {
        var (game, run) = Home(Gen(1));

        run.Refuel(1);
        run.Clear();

        Assert.False(run.HasWork);
        Assert.False(run.IsRefuelling(1));
    }
}
