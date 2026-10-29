using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>The fuel list: the order refuelling takes items in, and the promise that nothing off it is ever burned.</summary>
public class FuelOrderTests
{
    private static readonly long[] NoneRefused = Array.Empty<long>();

    [Fact]
    public void The_default_burns_junk_paper_before_cardboard_and_keeps_diesel_for_last()
    {
        var ids = FuelOrder.Parse(FuelOrder.Default).Ids;

        Assert.Equal(42001, ids[0]);
        Assert.True(ids.ToList().IndexOf(20005) < ids.ToList().IndexOf(20101), "wood chips (1500) burn before cardboard (3000)");
        Assert.Equal(40000, ids[^1]);
    }

    [Fact]
    public void Parsing_drops_junk_and_second_copies()
    {
        Assert.Equal("5,3", FuelOrder.Parse(" 5, x, 3,5,,-2,0").ToString());
        Assert.Empty(FuelOrder.Parse(null).Ids);
    }

    [Fact]
    public void The_earliest_item_on_the_list_in_storage_is_burned_next()
    {
        var order = FuelOrder.Parse("1,2,3");
        var stock = new[] { new FuelStack(100, 3, 5), new FuelStack(200, 2, 1) };

        Assert.Equal(200, order.Next(stock, NoneRefused)?.ItemId);
    }

    [Fact]
    public void An_item_off_the_list_is_never_burned()
    {
        var order = FuelOrder.Parse("1,2");
        var stock = new[] { new FuelStack(100, 9, 50) };

        Assert.Null(order.Next(stock, NoneRefused));
    }

    [Fact]
    public void A_refused_or_empty_stack_is_passed_over_for_the_next()
    {
        var order = FuelOrder.Parse("1,2");
        var stock = new[] { new FuelStack(100, 1, 0), new FuelStack(101, 1, 4), new FuelStack(200, 2, 4) };

        Assert.Equal(200, order.Next(stock, new long[] { 101 })?.ItemId);
    }

    [Fact]
    public void Moving_swaps_with_the_neighbour_and_stops_at_the_ends()
    {
        var order = FuelOrder.Parse("1,2,3");

        Assert.Equal("2,1,3", order.MoveUp(2).ToString());
        Assert.Equal("1,3,2", order.MoveDown(2).ToString());
        Assert.Equal("1,2,3", order.MoveUp(1).ToString());
        Assert.Equal("1,2,3", order.MoveDown(3).ToString());
        Assert.Equal("1,2,3", order.MoveUp(9).ToString());
    }

    [Fact]
    public void Adding_goes_last_and_removing_takes_it_off()
    {
        var order = FuelOrder.Parse("1,2");

        Assert.Equal("1,2,7", order.Add(7).ToString());
        Assert.Equal("1,2", order.Add(2).ToString());
        Assert.Equal("2", order.Remove(1).ToString());
    }
}
