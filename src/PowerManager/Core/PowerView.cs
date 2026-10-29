using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PowerManagerMod.Core;

/// <summary>One line of the fuel tab: an item, its name and how many are in home storage.</summary>
public readonly record struct FuelLine(int ConfigId, string Name, int Count);

/// <summary>
/// The JSON the panel draws from. Times are whole game minutes and power is the game's units, rounded; the page words
/// them. It is sent whole, and only when it changed, so it carries nothing that moves every frame.
/// </summary>
public static class PowerView
{
    public static string Json(
        PowerScene? scene,
        IReadOnlyCollection<DeviceKey> low,
        IReadOnlyCollection<DeviceKey> shed,
        Func<long, bool> isRefuelling,
        IReadOnlyList<FuelLine> order,
        IReadOnlyList<FuelLine> spare,
        PanelSettings settings)
    {
        var json = new StringBuilder(2048).Append('{');

        json.Append("\"game\":").Append(Flag(scene != null));

        if (scene != null)
        {
            var battery = scene.Battery;
            var forecast = Forecast.Of(battery);

            json.Append(",\"battery\":{")
                .Append("\"pct\":").Append(battery.Percent)
                .Append(",\"stored\":").Append(Whole(battery.Stored))
                .Append(",\"cap\":").Append(Whole(battery.Capacity))
                .Append(",\"gen\":").Append(Whole(battery.Generation))
                .Append(",\"cons\":").Append(Whole(battery.Consumption))
                .Append(",\"outage\":").Append(Flag(battery.IsOutage))
                .Append(",\"locked\":").Append(Flag(battery.IsLocked))
                .Append(",\"fc\":").Append(JsText.Literal(forecast.Kind.ToString()))
                .Append(",\"mins\":").Append(Whole(forecast.GameMinutes))
                .Append('}');

            json.Append(",\"gens\":[");

            for (var i = 0; i < scene.Generators.Count; i++)
            {
                var generator = scene.Generators[i];

                if (i > 0) json.Append(',');

                json.Append("{\"id\":").Append(JsText.Literal(Id(generator.Id)))
                    .Append(",\"name\":").Append(JsText.Literal(generator.Name))
                    .Append(",\"on\":").Append(Flag(generator.IsOn))
                    .Append(",\"slots\":").Append(generator.FuelSlots)
                    .Append(",\"max\":").Append(generator.MaxFuelSlots)
                    .Append(",\"mins\":").Append(Whole(generator.FuelSeconds / 60f))
                    .Append(",\"auto\":").Append(Flag(generator.AutoStart))
                    .Append(",\"at\":").Append((int)Math.Round(generator.AutoStartAt * 100f))
                    .Append(",\"busy\":").Append(Flag(isRefuelling(generator.Id)))
                    .Append('}');
            }

            json.Append("],\"devs\":[");

            for (var i = 0; i < scene.Devices.Count; i++)
            {
                var device = scene.Devices[i];

                if (i > 0) json.Append(',');

                json.Append("{\"id\":").Append(JsText.Literal(Id(device.Id)))
                    .Append(",\"name\":").Append(JsText.Literal(device.Name))
                    .Append(",\"on\":").Append(Flag(device.IsOn))
                    .Append(",\"w\":").Append(Tenths(device.Watts))
                    .Append(",\"low\":").Append(Flag(low.Contains(device.Key)))
                    .Append(",\"shed\":").Append(Flag(shed.Contains(device.Key)))
                    .Append(",\"managed\":").Append(Flag(device.IsManaged))
                    .Append('}');
            }

            json.Append(']');
        }

        json.Append(",\"order\":");
        Lines(json, order);
        json.Append(",\"spare\":");
        Lines(json, spare);

        json.Append(",\"set\":{")
            .Append("\"key\":").Append(JsText.Literal(settings.Key))
            .Append(",\"autoRefuel\":").Append(Flag(settings.AutoRefuel))
            .Append(",\"autoMinutes\":").Append(settings.AutoRefuelMinutes)
            .Append(",\"shedBelow\":").Append(settings.ShedBelow)
            .Append(",\"restoreAt\":").Append(settings.ShedBelow + Shedding.RestoreMargin)
            .Append('}');

        return json.Append('}').ToString();
    }

    /// <summary>
    /// The fuel tab's two lists: the fuel order with what storage holds of each, and every other burnable item in storage.
    /// </summary>
    public static (List<FuelLine> Order, List<FuelLine> Spare) FuelLines(FuelOrder order, IReadOnlyList<FuelStack> stock, Func<int, string> name)
    {
        var counts = new Dictionary<int, int>();

        foreach (var stack in stock) counts[stack.ConfigId] = (counts.TryGetValue(stack.ConfigId, out var held) ? held : 0) + Math.Max(0, stack.Count);

        var listed = new List<FuelLine>(order.Ids.Count);

        foreach (var id in order.Ids) listed.Add(new FuelLine(id, name(id), counts.TryGetValue(id, out var held) ? held : 0));

        var spare = new List<FuelLine>();

        foreach (var (id, count) in counts)
        {
            if (count > 0 && !order.Contains(id)) spare.Add(new FuelLine(id, name(id), count));
        }

        spare.Sort((a, b) => a.ConfigId.CompareTo(b.ConfigId));

        return (listed, spare);
    }

    private static void Lines(StringBuilder json, IReadOnlyList<FuelLine> lines)
    {
        json.Append('[');

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) json.Append(',');

            json.Append("{\"cfg\":").Append(lines[i].ConfigId.ToString(CultureInfo.InvariantCulture))
                .Append(",\"name\":").Append(JsText.Literal(lines[i].Name))
                .Append(",\"n\":").Append(lines[i].Count.ToString(CultureInfo.InvariantCulture))
                .Append('}');
        }

        json.Append(']');
    }

    /// <summary>Ids go as strings: a 64-bit id is past what a JavaScript number holds exactly.</summary>
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Whole(float value) => float.IsFinite(value) ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture) : "0";

    private static string Tenths(float value) => float.IsFinite(value) ? Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture) : "0";
}
