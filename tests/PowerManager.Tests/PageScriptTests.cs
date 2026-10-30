using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>page.js in a fake browser: the HUD button, the panel it draws from the plugin's JSON, and what its buttons send.</summary>
public class PageScriptTests
{
    private static readonly DeviceKey LampKey = new(1, 0, 0, 0);

    private static readonly PanelSettings Settings = new("F2", AutoRefuel: false, AutoRefuelMinutes: 60, ShedBelow: 20);

    private static string View(Battery? battery = null, Device? device = null, Generator? generator = null, IReadOnlyCollection<DeviceKey>? low = null, string? fuelJson = null)
    {
        var scene = new PowerScene(
            battery ?? new Battery(400f, 1000f, 10f, 30f, IsOutage: true, IsLocked: false),
            new[] { generator ?? new Generator(9007199254740993, "Diesel Generator", false, 1, 4, 3600f, false, 0.3f) },
            new[] { device ?? new Device(77, LampKey, "Lamp", true, 2.5f) },
            new GameLoad(1, 1));
        var order = new[] { new FuelLine(42001, "Shopping Receipt", 3, 300), new FuelLine(20101, "Cardboard", 0, 3000) };
        var spare = new[] { new FuelLine(11111, "Old Chair", 2, 1200), new FuelLine(15506, "High-Proof Alcohol", 0, 9000) };

        return PowerView.Json(scene, low ?? Array.Empty<DeviceKey>(), Array.Empty<DeviceKey>(), _ => false, fuelJson ?? PowerView.FuelJson(order, spare), Settings);
    }

    private static PageScript OpenPanel(string view, bool isChinese = false)
    {
        var page = new PageScript();

        page.Run(PageWords.Script(isChinese));
        page.Open(view);

        return page;
    }

    [Fact]
    public void In_a_game_the_bolt_button_goes_first_in_the_hud_tool_column_and_toggles_the_panel()
    {
        var page = new PageScript();

        page.Run(PageWords.Script(false));
        page.Run("openHud()");
        page.Live(true, "F2");
        page.Poll();

        Assert.True(page.Is("hudButton() && hudButton().parentNode.firstChild === hudButton()"));
        Assert.Equal("50", page.Text("hudButton().getAttribute('data-etk-dock')"));
        Assert.True(page.Is("isCoveredForClicks(hudButton())"));

        page.Click("hudButton()");

        Assert.Equal(new PageCommand[] { new PageCommand.Toggle() }, page.Sent);
    }

    [Fact]
    public void Out_of_a_game_the_bolt_button_is_taken_away()
    {
        var page = new PageScript();

        page.Run(PageWords.Script(false));
        page.Run("openHud()");
        page.Live(true, "F2");
        page.Poll();
        page.Live(false, "F2");
        page.Poll();

        Assert.False(page.Is("hudButton()"));
    }

    [Fact]
    public void Generator_buttons_send_the_full_id_and_the_new_auto_start_level()
    {
        var page = OpenPanel(View());

        page.Click("panelButton('start')");
        page.Status("Started Diesel Generator.");
        Assert.Equal(new PageCommand.Start(9007199254740993), page.Sent[0]);

        page.Click("panelButtonWith('autoat', '9007199254740993|0.4')");
        Assert.Contains(new PageCommand.AutoAt(9007199254740993, 0.4f), page.Sent);
    }

    [Fact]
    public void A_device_switch_and_its_priority_toggle_send_the_change()
    {
        var page = OpenPanel(View());

        page.Click("panelButtonWith('device', '77|0')");

        Assert.Equal(new PageCommand.Device(77, false), page.Sent.Single());
        Assert.True(page.Is("panelButtonWith('low', '77|1')"));
    }

    [Fact]
    public void A_defense_manager_device_has_its_switches_greyed_out()
    {
        var page = OpenPanel(View(device: new Device(77, LampKey, "Turret", true, 2.5f, IsManaged: true)));

        Assert.True(page.Is("panelButtonWith('device', '77|0').disabled"));
        Assert.True(page.Is("panelButtonWith('low', '77|1').disabled"));

        page.Click("panelButtonWith('device', '77|0')");

        Assert.Empty(page.Sent);
    }

    [Fact]
    public void While_the_game_holds_the_switches_device_switches_are_greyed_out()
    {
        var page = OpenPanel(View(battery: new Battery(400f, 1000f, 0f, 0f, IsOutage: true, IsLocked: true)));

        Assert.True(page.Is("panelButtonWith('device', '77|0').disabled"));
    }

    [Fact]
    public void The_fuel_tab_lists_the_order_and_moves_and_adds_items()
    {
        var page = OpenPanel(View());

        page.Click("panelButtonWith('tab', 'fuel')");

        Assert.True(page.Is("panelButtonWith('fuelup', '42001').disabled"), "the first item cannot move up");

        page.Click("panelButtonWith('fueldown', '42001')");
        Assert.Equal(new PageCommand.FuelDown(42001), page.Sent.Single());
        Assert.True(page.Is("panelButtonWith('fueladd', '11111')"));
    }

    [Fact]
    public void Every_fuel_line_in_both_lists_shows_its_burn_value()
    {
        var page = OpenPanel(View());

        page.Click("panelButtonWith('tab', 'fuel')");

        var order = page.Text("document.querySelector('.pm-order').textContent");
        var spare = page.Text("document.querySelector('.pm-spare').textContent");

        Assert.Contains("3 in storage, burn value 300", order);
        Assert.Contains("0 in storage, burn value 3000", order);
        Assert.Contains("2 in storage, burn value 1200", spare);
        Assert.Contains("0 in storage, burn value 9000", spare);
    }

    [Fact]
    public void An_item_removed_from_the_order_shows_in_the_add_list_when_the_next_view_arrives()
    {
        var items = new[] { new FuelItem(42001, 300), new FuelItem(20101, 3000) };
        var order = FuelOrder.Parse("42001,20101");
        var page = OpenPanel(View(fuelJson: FuelJson(order, items)));

        page.Click("panelButtonWith('tab', 'fuel')");
        Assert.True(page.Is("panelButtonWith('fueloff', '20101')"));
        Assert.False(page.Is("panelButtonWith('fueladd', '20101')"));

        page.Show(View(fuelJson: FuelJson(order.Remove(20101), items)));

        Assert.False(page.Is("panelButtonWith('fueloff', '20101')"));
        Assert.True(page.Is("panelButtonWith('fueladd', '20101')"));
    }

    private static string FuelJson(FuelOrder order, IReadOnlyList<FuelItem> items)
    {
        var (listed, spare) = PowerView.FuelLines(order, Array.Empty<FuelStack>(), items, id => "item " + id);

        return PowerView.FuelJson(listed, spare);
    }

    [Fact]
    public void The_settings_tab_steps_shedding_by_five()
    {
        var page = OpenPanel(View());

        page.Click("panelButtonWith('tab', 'settings')");
        page.Click("panelButtonWith('set', 'shedbelow|25')");

        Assert.Equal(new PageCommand.Setting(new SettingChange.ShedBelow(25)), page.Sent.Single());
    }

    [Fact]
    public void The_panel_shows_the_battery_forecast_in_words()
    {
        var page = OpenPanel(View());

        // 400 stored, losing 20 every 10 minutes: 200 minutes.
        Assert.Contains("Empty in 3 h 20 min", page.Text("drawingOf(document.body)"));
    }

    [Fact]
    public void In_chinese_the_panel_uses_the_chinese_words()
    {
        var page = OpenPanel(View(), isChinese: true);
        var drawing = page.Text("drawingOf(document.body)");

        Assert.Contains("全部启动", drawing);
        Assert.DoesNotContain("Start all", drawing);
    }

    [Fact]
    public void The_close_button_sends_close()
    {
        var page = OpenPanel(View());

        page.Click("panelButton('close')");

        Assert.Equal(new PageCommand[] { new PageCommand.Close() }, page.Sent);
    }

    [Fact]
    public void With_no_game_loaded_the_panel_says_to_load_one()
    {
        var page = OpenPanel(PowerView.Json(null, Array.Empty<DeviceKey>(), Array.Empty<DeviceKey>(), _ => false, PowerView.FuelJson(Array.Empty<FuelLine>(), Array.Empty<FuelLine>()), Settings));

        Assert.Contains("Load a game first.", page.Text("drawingOf(document.body)"));
    }
}
