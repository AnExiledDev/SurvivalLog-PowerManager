using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>Carrying the old mods' settings over, the devices file, and the power rules taken over from Game Rules.</summary>
public class ConfigAndRulesTests
{
    private const string GameRules = "## Settings file\n[Power]\n\n# comment\nBatteryMultiplier = 1.5\nDevicePowerUse = 1\nNoStormLeaks = true\n\n[Solar]\nSolarMultiplier = 2\nBadWeatherOutput = 0.5\n";
    private const string MachineSpeed = "[Machines]\r\nFuelBurn = 0.5\r\nCookingFuel = 2\r\n\r\n[Amounts]\r\nGeneratorEfficiency = \r\n";

    [Fact]
    public void The_operators_old_power_settings_carry_over_to_their_new_sections()
    {
        var carried = OldConfig.Carried(GameRules, MachineSpeed);

        Assert.Contains(new CarriedSetting("Solar", "SolarMultiplier", "2"), carried);
        Assert.Contains(new CarriedSetting("Solar", "BadWeatherOutput", "0.5"), carried);
        Assert.Contains(new CarriedSetting("Generators", "FuelBurn", "0.5"), carried);
        Assert.Contains(new CarriedSetting("Power", "BatteryMultiplier", "1.5"), carried);
    }

    [Fact]
    public void Settings_that_stay_in_the_old_mods_or_are_blank_are_not_carried()
    {
        var keys = OldConfig.Carried(GameRules, MachineSpeed).Select(setting => setting.Key).ToList();

        Assert.DoesNotContain("NoStormLeaks", keys);
        Assert.DoesNotContain("CookingFuel", keys);
        Assert.DoesNotContain("GeneratorEfficiency", keys);
    }

    [Fact]
    public void Missing_old_files_carry_nothing()
    {
        Assert.Empty(OldConfig.Carried(null, null));
    }

    [Fact]
    public void A_file_without_a_generators_setting_is_a_first_start()
    {
        Assert.False(OldConfig.HasSection("[Controls]\nPanelKey = F2\n[Generators]\n", "Generators"));
        Assert.True(OldConfig.HasSection("[Generators]\nAutoRefuel = false\n", "Generators"));
    }

    [Fact]
    public void Device_marks_are_kept_per_save_and_writing_one_save_leaves_the_others()
    {
        var lamp = DeviceKey.Of(5, 1.25, -2, 0.04);
        var file = DeviceFile.Write(null, "Save\tOne", new DeviceMarks(new[] { lamp }, Array.Empty<DeviceKey>()));

        file = DeviceFile.Write(file, "Two", new DeviceMarks(Array.Empty<DeviceKey>(), new[] { lamp }));

        var one = DeviceFile.Read(file, "Save\tOne");
        var two = DeviceFile.Read(file, "Two");

        Assert.Equal(new[] { lamp }, one.LowPriority);
        Assert.Empty(one.Shed);
        Assert.Equal(new[] { lamp }, two.Shed);
        Assert.StartsWith(DeviceFile.Header, file);
    }

    [Fact]
    public void A_device_rebuilt_in_the_same_spot_keeps_its_key()
    {
        Assert.Equal(DeviceKey.Of(5, 1.249, 0, 0), DeviceKey.Of(5, 1.21, 0, 0));
        Assert.True(DeviceKey.TryParse(DeviceKey.Of(5, -3.5, 1, 2).ToString(), out var parsed));
        Assert.Equal(DeviceKey.Of(5, -3.5, 1, 2), parsed);
    }

    [Fact]
    public void Bad_weather_gives_the_set_share_of_a_cloudy_day_then_the_multiplier()
    {
        var rules = PowerRules.Game with { BadWeatherSolar = 0.5f, SolarMultiplier = 2f };
        var result = SolarRules.Apply(new SolarReading(0f, 10f, 1f, 1f, IsBadWeather: true), rules);

        Assert.Equal(10f, result.Power, 3);
        Assert.True(result.ClearsBadWeather);
    }

    [Fact]
    public void A_panel_where_no_light_reaches_stays_dark_in_bad_weather()
    {
        var rules = PowerRules.Game with { BadWeatherSolar = 1f };

        Assert.False(SolarRules.Apply(new SolarReading(0f, 10f, 0f, 1f, IsBadWeather: true), rules).ClearsBadWeather);
    }

    [Fact]
    public void Only_batteries_are_scaled()
    {
        Assert.Equal(300f, BatteryCapacity.Scale(BatteryCapacity.BatteryType, 100f, 3f));
        Assert.Equal(100f, BatteryCapacity.Scale(3, 100f, 3f));
    }

    [Theory]
    [InlineData(40019, 15, 2, false, true)]
    [InlineData(40019, 16, 2, false, false)]
    [InlineData(52019, 99, 0, true, true)]
    [InlineData(40019, 5, 0, false, false)]
    [InlineData(12345, 1, 0, true, false)]
    public void The_power_cut_waits_for_its_delay_or_never_comes(int eventId, int day, int delay, bool never, bool blocked)
    {
        Assert.Equal(blocked, PowerCutBlock.Blocks(eventId, day, PowerRules.Game with { PowerCutDelay = delay, NoPowerCut = never }));
    }

    [Fact]
    public void A_day_that_cannot_be_read_lets_a_delayed_cut_through()
    {
        Assert.False(PowerCutBlock.Blocks(40019, null, PowerRules.Game with { PowerCutDelay = 5 }));
    }
}
