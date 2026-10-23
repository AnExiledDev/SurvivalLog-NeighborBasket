using System;
using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>What the neighbor wants this round, as the basket screen shows it.</summary>
public sealed record BasketWish(bool IsActive, bool IsPending, bool IsAway, bool IsDying, IReadOnlyList<int> Categories, int? CrisisCategory)
{
    public static readonly BasketWish None = new(false, false, false, false, new int[0], null);
}

/// <summary>
/// One stack the player owns at home or in the bag. UnitValue is the game's trade value of one unit; Weight is the
/// whole stack's weight in the game's own units (0 when it weighs nothing).
/// </summary>
public sealed record Candidate(long ItemId, long OwnerId, int ConfigId, int Category, int UnitValue, int Count, bool IsForbidden, int Weight = 0)
{
    /// <summary>Forbidden items are refused by the basket, and items with no trade value are scored by special rules, so neither is offered.</summary>
    public bool IsUsable => !IsForbidden && UnitValue > 0 && Count > 0;

    /// <summary>What one unit weighs, rounded up so a pick never weighs less than the game will count.</summary>
    public int UnitWeight => Count <= 1 ? Weight : (Weight + Count - 1) / Count;
}

/// <summary>A stack to put in the basket; IsOneUnit moves a single unit of it, otherwise the whole stack.</summary>
public sealed record Pick(Candidate Item, bool IsOneUnit)
{
    public int Weight => IsOneUnit ? Item.UnitWeight : Item.Weight;
}

/// <summary>
/// The room left in the basket: cells, weight (infinite when the game does not weigh it), and how many extra items
/// may go in after one of each wished kind.
/// </summary>
public sealed record Room(int FreeCells, float FreeWeight, int Extras);

public enum FillStop
{
    None,
    NoNeighbor,
    Away,
    Pending,
    Full,
    AllThere,
}

/// <summary>
/// The picks, the wished categories nothing at hand could cover, and why nothing was picked when nothing was.
/// TooHeavy counts the wished categories that had items, all of them too heavy for the weight left.
/// </summary>
public sealed record FillResult(IReadOnlyList<Pick> Picks, IReadOnlyList<int> Missing, FillStop Stop, bool IsFoodOnly, int TooHeavy = 0);

/// <summary>
/// Chooses what goes in the basket. Each wished category she gets earns a flat bonus, while value counts for little,
/// so one cheap item per wished category beats a few dear ones; the crisis category goes first because one piece of it
/// resolves the crisis. After that, extra items of the wished kinds fill the room left, the most value for their weight
/// first, since wished items count more than others and the basket's limit is its weight. While she is dying only food
/// counts, so the room is filled with food, dearest first, since a dearer dish tends to fill her more.
/// </summary>
public static class FillPlan
{
    public const int FoodCategory = 1;

    public static FillResult Plan(BasketWish wish, IReadOnlyList<Candidate> sources, IReadOnlyCollection<int> categoriesInBasket, Room room, bool movesOneUnit)
    {
        if (!wish.IsActive) return Stopped(FillStop.NoNeighbor);
        if (wish.IsAway) return Stopped(FillStop.Away);
        if (room.FreeCells <= 0 || room.FreeWeight <= 0) return Stopped(FillStop.Full);
        if (wish.IsDying) return FillWithFood(sources, room, movesOneUnit);

        var wished = Wanted(wish).ToList();
        var wanted = wished.Where(category => !categoriesInBasket.Contains(category)).ToList();

        if (wished.Count == 0) return Stopped(FillStop.Pending);

        var picks = new List<Pick>();
        var missing = new List<int>();
        var tooHeavy = 0;
        var weightLeft = room.FreeWeight;

        foreach (var category in wanted)
        {
            var usable = sources.Where(item => item.IsUsable && item.Category == category).ToList();
            var cheapest = usable.Where(item => Fits(item, weightLeft, movesOneUnit)).OrderBy(item => Cost(item, movesOneUnit)).ThenBy(item => item.Count).ThenBy(item => item.ItemId).FirstOrDefault();

            if (cheapest == null)
            {
                if (usable.Count == 0) missing.Add(category);
                else tooHeavy++;

                continue;
            }

            if (picks.Count >= room.FreeCells) continue;

            var pick = new Pick(cheapest, movesOneUnit);
            picks.Add(pick);
            weightLeft -= pick.Weight;
        }

        AddExtras(picks, sources, wished, room, weightLeft, movesOneUnit);

        if (picks.Count == 0 && missing.Count == 0 && tooHeavy == 0) return Stopped(wanted.Count == 0 ? FillStop.AllThere : FillStop.Full);

        return new FillResult(picks, missing, FillStop.None, false, tooHeavy);
    }

    /// <summary>
    /// Up to room.Extras more stacks of the wished kinds, one pick each, the most value per weight first, while cells
    /// and weight last. A stack already picked is not picked again.
    /// </summary>
    private static void AddExtras(List<Pick> picks, IReadOnlyList<Candidate> sources, IReadOnlyCollection<int> wished, Room room, float weightLeft, bool movesOneUnit)
    {
        var taken = picks.Select(pick => pick.Item.ItemId).ToHashSet();
        var extras = sources
            .Where(item => item.IsUsable && wished.Contains(item.Category) && !taken.Contains(item.ItemId))
            .OrderByDescending(item => ValuePerWeight(item, movesOneUnit))
            .ThenByDescending(item => item.UnitValue)
            .ThenBy(item => item.ItemId);
        var added = 0;

        foreach (var item in extras)
        {
            if (added >= room.Extras || picks.Count >= room.FreeCells) return;

            var pick = new Pick(item, movesOneUnit);

            if (pick.Weight > weightLeft) continue;

            picks.Add(pick);
            weightLeft -= pick.Weight;
            added++;
        }
    }

    /// <summary>The crisis category first, then her wishes in the order she named them, each once.</summary>
    private static IEnumerable<int> Wanted(BasketWish wish)
    {
        var seen = new HashSet<int>();

        if (wish.CrisisCategory is { } crisis && seen.Add(crisis)) yield return crisis;

        if (wish.IsPending) yield break;

        foreach (var category in wish.Categories)
        {
            if (seen.Add(category)) yield return category;
        }
    }

    private static FillResult FillWithFood(IReadOnlyList<Candidate> sources, Room room, bool movesOneUnit)
    {
        var food = sources.Where(item => item.IsUsable && item.Category == FoodCategory).OrderByDescending(item => item.UnitValue).ThenBy(item => item.Count).ThenBy(item => item.ItemId).ToList();
        var picks = new List<Pick>();
        var weightLeft = room.FreeWeight;

        foreach (var item in food)
        {
            if (picks.Count >= room.FreeCells) break;

            var pick = new Pick(item, movesOneUnit);

            if (pick.Weight > weightLeft) continue;

            picks.Add(pick);
            weightLeft -= pick.Weight;
        }

        return new FillResult(picks, food.Count == 0 ? new[] { FoodCategory } : new int[0], picks.Count == 0 && food.Count > 0 ? FillStop.Full : FillStop.None, true);
    }

    private static bool Fits(Candidate item, float weightLeft, bool movesOneUnit) => new Pick(item, movesOneUnit).Weight <= weightLeft;

    /// <summary>What the move costs the player: one unit, or the whole stack when only whole stacks move.</summary>
    private static long Cost(Candidate item, bool movesOneUnit) => (long)item.UnitValue * (movesOneUnit ? 1 : item.Count);

    private static double ValuePerWeight(Candidate item, bool movesOneUnit) => (double)Cost(item, movesOneUnit) / Math.Max(1, new Pick(item, movesOneUnit).Weight);

    private static FillResult Stopped(FillStop stop) => new(new Pick[0], new int[0], stop, false);
}
