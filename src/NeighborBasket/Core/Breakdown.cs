using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>
/// What an item's own use does, read from the game's item, effect and package tables. UseAction is the action its Use
/// starts (0 for none), HasOtherEffect is true when the use also feeds, heals, buffs or scores, IsRandom is true when
/// any package it gives rolls a random pick, and OutputCategories holds the category of every item it gives.
/// </summary>
public sealed record UseInfo(bool CantUse, int UseAction, bool HasOtherEffect, bool IsRandom, IReadOnlyList<int> OutputCategories);

/// <summary>
/// Which items the player can break down by hand: the game's own Use takes them apart (the wind chime's "take apart",
/// the marble's "smash") and gives nothing but materials back. Food, medicine, a flower that turns into fertilizer and
/// furniture packages all fail the test, so only the trinkets and notes the neighbors send pass.
/// </summary>
public static class Breakdown
{
    /// <summary>The game's category for materials (tin sheet, scrap plastic, broken glass, paper scraps).</summary>
    public const int MaterialCategory = 9;

    public static bool IsBreakable(UseInfo? use) =>
        use is { CantUse: false, UseAction: > 0, HasOtherEffect: false, IsRandom: false } &&
        use.OutputCategories.Count > 0 && use.OutputCategories.All(category => category == MaterialCategory);
}
