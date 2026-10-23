using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>What the neighbor wants this round, as the basket screen shows it.</summary>
public sealed record BasketWish(bool IsActive, bool IsPending, bool IsAway, bool IsDying, IReadOnlyList<int> Categories, int? CrisisCategory)
{
    public static readonly BasketWish None = new(false, false, false, false, new int[0], null);
}

/// <summary>One stack the player owns at home or in the bag. UnitValue is the game's trade value of one unit.</summary>
public sealed record Candidate(long ItemId, long OwnerId, int ConfigId, int Category, int UnitValue, int Count, bool IsForbidden)
{
    /// <summary>Forbidden items are refused by the basket, and items with no trade value are scored by special rules, so neither is offered.</summary>
    public bool IsUsable => !IsForbidden && UnitValue > 0 && Count > 0;
}

/// <summary>A stack to put in the basket; IsOneUnit moves a single unit of it, otherwise the whole stack.</summary>
public sealed record Pick(Candidate Item, bool IsOneUnit);

public enum FillStop
{
    None,
    NoNeighbor,
    Away,
    Pending,
    Full,
    AllThere,
}

/// <summary>The picks, the wished categories nothing at hand could cover, and why nothing was picked when nothing was.</summary>
public sealed record FillResult(IReadOnlyList<Pick> Picks, IReadOnlyList<int> Missing, FillStop Stop, bool IsFoodOnly);

/// <summary>
/// Chooses what goes in the basket. Each wished category she gets earns a flat bonus, while value counts for little,
/// so one cheap item per wished category beats a few dear ones; the crisis category goes first because one piece of it
/// resolves the crisis. While she is dying only food counts, so the free cells are filled with food, dearest first,
/// since a dearer dish tends to fill her more.
/// </summary>
public static class FillPlan
{
    public const int FoodCategory = 1;

    public static FillResult Plan(BasketWish wish, IReadOnlyList<Candidate> sources, IReadOnlyCollection<int> categoriesInBasket, int freeCells, bool movesOneUnit)
    {
        if (!wish.IsActive) return Stopped(FillStop.NoNeighbor);
        if (wish.IsAway) return Stopped(FillStop.Away);
        if (freeCells <= 0) return Stopped(FillStop.Full);
        if (wish.IsDying) return FillWithFood(sources, freeCells, movesOneUnit);

        var wanted = Wanted(wish).Where(category => !categoriesInBasket.Contains(category)).ToList();

        if (wanted.Count == 0) return Stopped(wish.IsPending && wish.CrisisCategory == null ? FillStop.Pending : FillStop.AllThere);

        var picks = new List<Pick>();
        var missing = new List<int>();

        foreach (var category in wanted)
        {
            var cheapest = sources.Where(item => item.IsUsable && item.Category == category).OrderBy(item => Cost(item, movesOneUnit)).ThenBy(item => item.Count).ThenBy(item => item.ItemId).FirstOrDefault();

            if (cheapest == null) missing.Add(category);
            else if (picks.Count < freeCells) picks.Add(new Pick(cheapest, movesOneUnit));
        }

        return new FillResult(picks, missing, FillStop.None, false);
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

    private static FillResult FillWithFood(IReadOnlyList<Candidate> sources, int freeCells, bool movesOneUnit)
    {
        var picks = sources.Where(item => item.IsUsable && item.Category == FoodCategory).OrderByDescending(item => item.UnitValue).ThenBy(item => item.Count).ThenBy(item => item.ItemId).Take(freeCells).Select(item => new Pick(item, movesOneUnit)).ToList();

        return new FillResult(picks, picks.Count == 0 ? new[] { FoodCategory } : new int[0], FillStop.None, true);
    }

    /// <summary>What the move costs the player: one unit, or the whole stack when only whole stacks move.</summary>
    private static long Cost(Candidate item, bool movesOneUnit) => (long)item.UnitValue * (movesOneUnit ? 1 : item.Count);

    private static FillResult Stopped(FillStop stop) => new(new Pick[0], new int[0], stop, false);
}
