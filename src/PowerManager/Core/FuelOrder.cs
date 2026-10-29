using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PowerManagerMod.Core;

/// <summary>
/// The items generators are refuelled with, by item id, first used first. An item left off the list is never used, so
/// food, tools and anything else burnable stays where it is. Saved as a comma-separated list in the config.
/// </summary>
public sealed class FuelOrder
{
    /// <summary>
    /// Junk paper first (notes, letters, flyers and the like, which have no other use), then the common fuels from the
    /// weakest to the strongest, so the best fuel is kept for last.
    /// </summary>
    public const string Default =
        "42001,42002,42003,42006,42007,42008,42011,42012,42013,42014,42015,42016,42017,42018,42019,42020,42021,42026,42028," +
        "42032,42033,42034,42035,42036,42039,42040,42042,42044,42045,42046,11045,20001,2506,2505,20005,20101,40000";

    private readonly int[] _ids;

    private FuelOrder(int[] ids)
    {
        _ids = ids;
    }

    public IReadOnlyList<int> Ids => _ids;

    /// <summary>Ids that are not whole numbers above 0 are dropped, and so is a second copy of an id.</summary>
    public static FuelOrder Parse(string? text)
    {
        var ids = (text ?? "")
            .Split(',')
            .Select(part => int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return new FuelOrder(ids);
    }

    public bool Contains(int configId) => Array.IndexOf(_ids, configId) >= 0;

    public FuelOrder MoveUp(int configId) => Swap(configId, -1);

    public FuelOrder MoveDown(int configId) => Swap(configId, +1);

    public FuelOrder Remove(int configId) => new(_ids.Where(id => id != configId).ToArray());

    /// <summary>Added at the end, where it is used last; an id already on the list stays where it is.</summary>
    public FuelOrder Add(int configId) => configId <= 0 || Contains(configId) ? this : new FuelOrder(_ids.Append(configId).ToArray());

    /// <summary>
    /// The stack to burn next: one of the earliest item on the list that is in storage. A stack the game refused this
    /// round is skipped, so one bad stack does not stop the rest being used.
    /// </summary>
    public FuelStack? Next(IReadOnlyList<FuelStack> stock, IReadOnlyCollection<long> refused)
    {
        FuelStack? best = null;
        var bestRank = int.MaxValue;

        foreach (var stack in stock)
        {
            if (stack.Count <= 0 || refused.Contains(stack.ItemId)) continue;

            var rank = Array.IndexOf(_ids, stack.ConfigId);

            if (rank < 0 || rank >= bestRank) continue;

            best = stack;
            bestRank = rank;
        }

        return best;
    }

    public override string ToString() => string.Join(",", _ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    private FuelOrder Swap(int configId, int step)
    {
        var from = Array.IndexOf(_ids, configId);
        var to = from + step;

        if (from < 0 || to < 0 || to >= _ids.Length) return this;

        var ids = (int[])_ids.Clone();

        (ids[from], ids[to]) = (ids[to], ids[from]);

        return new FuelOrder(ids);
    }
}
