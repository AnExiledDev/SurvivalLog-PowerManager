using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>
/// The fuel tab's two lists. The add list offers only what a generator can burn, never an item already on the order, and
/// an item taken off the order can be added back at once, whether storage holds any or not.
/// </summary>
public class FuelLinesTests
{
    private static readonly FuelItem Receipt = new(42001, 300);
    private static readonly FuelItem Cardboard = new(20101, 3000);
    private static readonly FuelItem HighProofAlcohol = new(15506, 9000);
    private static readonly FuelItem Diesel = new(40000, 48000);

    private static readonly FuelItem[] Burnable = { Diesel, HighProofAlcohol, Receipt, Cardboard };

    private static string Name(int id) => "item " + id;

    [Fact]
    public void The_add_list_holds_only_items_with_a_burn_value()
    {
        var items = new[] { Receipt, new FuelItem(777, 0) };
        var stock = new[] { new FuelStack(1, 999, 5), new FuelStack(2, 777, 3) };

        var (_, spare) = PowerView.FuelLines(FuelOrder.Parse(""), stock, items, Name);

        Assert.Equal(new[] { Receipt.ConfigId }, spare.Select(line => line.ConfigId));
    }

    [Fact]
    public void The_add_list_offers_every_burnable_item_even_one_storage_does_not_hold()
    {
        var (_, spare) = PowerView.FuelLines(FuelOrder.Parse(""), Array.Empty<FuelStack>(), Burnable, Name);

        Assert.Equal(4, spare.Count);
        Assert.All(spare, line => Assert.Equal(0, line.Count));
    }

    [Fact]
    public void An_item_on_the_order_is_not_in_the_add_list()
    {
        var (order, spare) = PowerView.FuelLines(FuelOrder.Parse("42001,40000"), Array.Empty<FuelStack>(), Burnable, Name);

        Assert.Equal(new[] { 42001, 40000 }, order.Select(line => line.ConfigId));
        Assert.Equal(new[] { Cardboard.ConfigId, HighProofAlcohol.ConfigId }, spare.Select(line => line.ConfigId));
    }

    [Fact]
    public void An_item_removed_from_the_order_is_back_in_the_add_list()
    {
        var order = FuelOrder.Parse("42001,20101");

        var (_, before) = PowerView.FuelLines(order, Array.Empty<FuelStack>(), Burnable, Name);
        var (listed, after) = PowerView.FuelLines(order.Remove(Cardboard.ConfigId), Array.Empty<FuelStack>(), Burnable, Name);

        Assert.DoesNotContain(before, line => line.ConfigId == Cardboard.ConfigId);
        Assert.Contains(after, line => line.ConfigId == Cardboard.ConfigId && line.Burn == 3000);
        Assert.DoesNotContain(listed, line => line.ConfigId == Cardboard.ConfigId);
    }

    [Fact]
    public void Both_lists_carry_each_items_burn_value_and_what_storage_holds()
    {
        var stock = new[] { new FuelStack(1, 42001, 2), new FuelStack(2, 42001, 3), new FuelStack(3, 15506, 1) };

        var (order, spare) = PowerView.FuelLines(FuelOrder.Parse("42001"), stock, Burnable, Name);

        Assert.Equal(new FuelLine(42001, "item 42001", 5, 300), order.Single());
        Assert.Contains(new FuelLine(15506, "item 15506", 1, 9000), spare);
    }

    [Fact]
    public void An_order_item_the_game_does_not_burn_shows_a_burn_value_of_zero()
    {
        var (order, _) = PowerView.FuelLines(FuelOrder.Parse("555"), Array.Empty<FuelStack>(), Burnable, Name);

        Assert.Equal(0, order.Single().Burn);
    }

    [Fact]
    public void The_add_list_goes_from_the_weakest_fuel_to_the_strongest()
    {
        var (_, spare) = PowerView.FuelLines(FuelOrder.Parse(""), Array.Empty<FuelStack>(), Burnable, Name);

        Assert.Equal(new[] { 300, 3000, 9000, 48000 }, spare.Select(line => line.Burn));
    }

    [Fact]
    public void The_view_sends_each_lines_burn_value()
    {
        var json = PowerView.FuelJson(new[] { new FuelLine(42001, "Receipt", 2, 300) }, new[] { new FuelLine(40000, "Diesel", 0, 48000) });

        Assert.Equal("\"order\":[{\"cfg\":42001,\"name\":\"Receipt\",\"n\":2,\"burn\":300}],\"spare\":[{\"cfg\":40000,\"name\":\"Diesel\",\"n\":0,\"burn\":48000}]", json);
    }
}
