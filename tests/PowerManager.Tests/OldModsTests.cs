using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>
/// Game Rules and Machine Speed before 0.7.0 still apply the power settings, so Power Manager must leave them alone
/// beside those versions or the player gets every setting twice.
/// </summary>
public class OldModsTests
{
    [Theory]
    [InlineData("0.6.2")]
    [InlineData("0.5.4")]
    [InlineData("0.6.99")]
    public void Versions_that_still_have_the_power_settings_are_old(string version)
    {
        Assert.True(OldMods.IsOld(version));
    }

    [Theory]
    [InlineData("0.7.0")]
    [InlineData("0.7.1")]
    [InlineData("0.8.1")]
    [InlineData("1.0.0")]
    public void Versions_without_the_power_settings_are_not_old(string version)
    {
        Assert.False(OldMods.IsOld(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a version")]
    public void A_mod_that_is_not_installed_or_unreadable_does_not_hold_back_the_settings(string? version)
    {
        Assert.False(OldMods.IsOld(version));
    }

    [Fact]
    public void A_prerelease_tag_is_read_by_its_number()
    {
        Assert.True(OldMods.IsOld("0.6.3-beta"));
    }

    [Fact]
    public void The_toast_names_exactly_the_mods_to_update()
    {
        Assert.Equal("Game Rules and Machine Speed", OldMods.Names(true, true));
        Assert.Equal("Game Rules", OldMods.Names(true, false));
        Assert.Equal("Machine Speed", OldMods.Names(false, true));
        Assert.Equal("", OldMods.Names(false, false));
    }
}
