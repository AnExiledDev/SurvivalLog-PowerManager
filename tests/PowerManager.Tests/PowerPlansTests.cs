using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>The battery forecast, load shedding and when auto-refuel steps in.</summary>
public class PowerPlansTests
{
    private static Battery Cut(float stored, float cap = 1000f, float gen = 0f, float cons = 0f) => new(stored, cap, gen, cons, IsOutage: true, IsLocked: false);

    private static readonly DeviceKey LampKey = new(1, 0, 0, 0);
    private static readonly DeviceKey FridgeKey = new(2, 0, 0, 0);

    private static Device Lamp(bool isOn = true, bool isManaged = false) => new(10, LampKey, "Lamp", isOn, 5f, isManaged);

    private static Device Fridge(bool isOn = true) => new(11, FridgeKey, "Fridge", isOn, 8f);

    [Fact]
    public void A_draining_battery_says_how_many_game_minutes_it_has_left()
    {
        var forecast = Forecast.Of(Cut(500f, gen: 10f, cons: 60f));

        Assert.Equal(ForecastKind.Draining, forecast.Kind);
        Assert.Equal(100f, forecast.GameMinutes, 3);
    }

    [Fact]
    public void A_charging_battery_says_how_long_until_full()
    {
        var forecast = Forecast.Of(Cut(800f, gen: 50f, cons: 30f));

        Assert.Equal(ForecastKind.Charging, forecast.Kind);
        Assert.Equal(100f, forecast.GameMinutes, 3);
    }

    [Fact]
    public void City_power_no_battery_and_the_ends_have_no_time()
    {
        Assert.Equal(ForecastKind.CityPower, Forecast.Of(new Battery(5f, 10f, 0f, 9f, IsOutage: false, IsLocked: false)).Kind);
        Assert.Equal(ForecastKind.NoBattery, Forecast.Of(Cut(0f, cap: 0f, cons: 5f)).Kind);
        Assert.Equal(ForecastKind.Empty, Forecast.Of(Cut(0f, cons: 5f)).Kind);
        Assert.Equal(ForecastKind.Full, Forecast.Of(Cut(1000f, gen: 5f)).Kind);
        Assert.Equal(ForecastKind.Steady, Forecast.Of(Cut(300f, gen: 5f, cons: 5f)).Kind);
    }

    [Fact]
    public void Below_the_level_in_a_power_cut_only_low_priority_devices_that_are_on_are_switched_off()
    {
        var plan = Shedding.Plan(Cut(150f), new[] { Lamp(), Fridge() }, new[] { LampKey }, Array.Empty<DeviceKey>(), shedBelow: 20);

        Assert.Equal(new[] { 10L }, plan.SwitchOff.Select(device => device.Id));
        Assert.Empty(plan.SwitchOn);
    }

    [Fact]
    public void A_device_defense_manager_runs_is_never_shed()
    {
        var plan = Shedding.Plan(Cut(150f), new[] { Lamp(isManaged: true) }, new[] { LampKey }, Array.Empty<DeviceKey>(), shedBelow: 20);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Between_the_level_and_ten_points_over_nothing_comes_back_yet()
    {
        var plan = Shedding.Plan(Cut(250f), new[] { Lamp(isOn: false) }, new[] { LampKey }, new[] { LampKey }, shedBelow: 20);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Ten_points_over_only_the_devices_the_mod_switched_off_come_back()
    {
        var plan = Shedding.Plan(Cut(300f), new[] { Lamp(isOn: false), Fridge(isOn: false) }, new[] { LampKey, FridgeKey }, new[] { LampKey }, shedBelow: 20);

        Assert.Equal(new[] { 10L }, plan.SwitchOn.Select(device => device.Id));
        Assert.Empty(plan.SwitchOff);
    }

    [Fact]
    public void City_power_back_or_shedding_turned_off_gives_the_devices_back()
    {
        var cityBack = new Battery(50f, 1000f, 0f, 0f, IsOutage: false, IsLocked: false);

        Assert.Single(Shedding.Plan(cityBack, new[] { Lamp(isOn: false) }, new[] { LampKey }, new[] { LampKey }, shedBelow: 20).SwitchOn);
        Assert.Single(Shedding.Plan(Cut(50f), new[] { Lamp(isOn: false) }, new[] { LampKey }, new[] { LampKey }, shedBelow: 0).SwitchOn);
    }

    [Fact]
    public void Nothing_is_switched_while_the_game_holds_the_switches()
    {
        var locked = new Battery(50f, 1000f, 0f, 0f, IsOutage: true, IsLocked: true);

        Assert.True(Shedding.Plan(locked, new[] { Lamp() }, new[] { LampKey }, Array.Empty<DeviceKey>(), shedBelow: 20).IsEmpty);
    }

    [Theory]
    [InlineData(true, false, 1000f, true)]
    [InlineData(false, true, 1000f, true)]
    [InlineData(false, false, 1000f, false)]
    [InlineData(true, false, 4000f, false)]
    public void Auto_refuel_tops_up_a_low_generator_that_is_running_or_set_to_start(bool isOn, bool autoStart, float fuelSeconds, bool expected)
    {
        var generator = new Generator(1, "Gen", isOn, 1, 4, fuelSeconds, autoStart, 0.3f);

        Assert.Equal(expected, AutoRefuel.Wants(generator, minutes: 60));
    }

    [Fact]
    public void Auto_refuel_leaves_a_full_generator_alone()
    {
        Assert.False(AutoRefuel.Wants(new Generator(1, "Gen", true, 4, 4, 10f, false, 0.3f), minutes: 60));
    }
}
