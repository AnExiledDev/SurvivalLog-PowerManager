using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>What the page sends, parsed the way the plugin acts on it; anything malformed is logged, never acted on.</summary>
public class PageCommandTests
{
    private static PageCommand Parse(string verb) => PageCommand.Parse(PageCommand.Prefix + verb);

    [Fact]
    public void Generator_commands_carry_the_full_64_bit_id()
    {
        Assert.Equal(new PageCommand.Start(9007199254740993), Parse("start|9007199254740993"));
        Assert.Equal(new PageCommand.Stop(-5), Parse("stop|-5"));
        Assert.Equal(new PageCommand.Auto(7, true), Parse("auto|7|1"));
        Assert.Equal(new PageCommand.Refuel(7), Parse("refuel|7"));
        Assert.Equal(new PageCommand.Device(8, false), Parse("device|8|0"));
        Assert.Equal(new PageCommand.Low(8, true), Parse("low|8|1"));
    }

    [Theory]
    [InlineData("0.3", 0.3f)]
    [InlineData("0.05", 0.1f)]
    [InlineData("2", 0.9f)]
    [InlineData("0.44", 0.4f)]
    public void An_auto_start_level_is_kept_to_the_games_range_in_tenths(string sent, float expected)
    {
        var command = Assert.IsType<PageCommand.AutoAt>(Parse("autoat|7|" + sent));

        Assert.Equal(expected, command.At, 3);
    }

    [Theory]
    [InlineData("auto|7|2")]
    [InlineData("autoat|7|NaN")]
    [InlineData("start|x")]
    [InlineData("start")]
    [InlineData("fuelup|0")]
    [InlineData("fueladd|-3")]
    [InlineData("set|autominutes|lots")]
    [InlineData("set|colour|1")]
    [InlineData("explode")]
    public void A_malformed_command_is_unknown(string verb)
    {
        Assert.IsType<PageCommand.Unknown>(Parse(verb));
    }

    [Fact]
    public void A_message_for_another_mod_is_not_ours()
    {
        Assert.False(PageCommand.IsOurs("slmod|defensemanager|toggle"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|defensemanager|toggle"));
    }

    [Theory]
    [InlineData("autominutes", "65", 70)]
    [InlineData("autominutes", "0", 10)]
    [InlineData("autominutes", "99999", 1440)]
    public void Auto_refuel_minutes_are_kept_in_range_and_to_the_step(string name, string value, int expected)
    {
        Assert.Equal(new SettingChange.AutoRefuelMinutes(expected), SettingChange.Parse(name, value));
    }

    [Theory]
    [InlineData("-5", 0)]
    [InlineData("23", 25)]
    [InlineData("95", 90)]
    public void Shed_level_is_kept_in_range_and_to_the_step(string value, int expected)
    {
        Assert.Equal(new SettingChange.ShedBelow(expected), SettingChange.Parse("shedbelow", value));
    }

    [Fact]
    public void Settings_arrive_as_setting_commands()
    {
        Assert.Equal(new PageCommand.Setting(new SettingChange.AutoRefuel(true)), Parse("set|autorefuel|1"));
        Assert.Equal(new PageCommand.FuelUp(42001), Parse("fuelup|42001"));
        Assert.Equal(new PageCommand.FuelAdd(20101), Parse("fueladd|20101"));
    }
}
