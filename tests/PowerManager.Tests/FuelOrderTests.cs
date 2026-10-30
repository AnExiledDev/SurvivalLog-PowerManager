using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>The fuel list: the order refuelling takes items in, and the promise that nothing off it is ever burned.</summary>
public class FuelOrderTests
{
    private static readonly long[] NoneRefused = Array.Empty<long>();

    private const int ScrapPaper = 20001;
    private const int Newspaper = 2506;
    private const int EntertainingMagazine = 2505;
    private const int WoodChips = 20005;
    private const int Cardboard = 20101;
    private const int PoorAlcohol = 15505;
    private const int CommonAlcohol = 15504;
    private const int HighProofAlcohol = 15506;
    private const int Diesel = 40000;

    [Fact]
    public void The_default_burns_junk_paper_first_then_the_alcohols_weakest_first_and_keeps_diesel_for_last()
    {
        var ids = FuelOrder.Parse(FuelOrder.Default).Ids.ToList();

        Assert.Equal(42001, ids[0]);
        Assert.Equal(new[] { PoorAlcohol, CommonAlcohol, HighProofAlcohol, Diesel }, ids.Skip(ids.Count - 4));
    }

    [Fact]
    public void The_default_leaves_scrap_paper_newspaper_magazines_wood_chips_and_cardboard_off()
    {
        var order = FuelOrder.Parse(FuelOrder.Default);

        foreach (var id in new[] { ScrapPaper, Newspaper, EntertainingMagazine, WoodChips, Cardboard }) Assert.False(order.Contains(id), $"{id} is on the default list");
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
