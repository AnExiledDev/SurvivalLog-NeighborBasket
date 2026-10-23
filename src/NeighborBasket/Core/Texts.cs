using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>One piece of on-screen text in the two languages the game ships.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Say(bool isChinese, params object[] values) => string.Format(Pick(isChinese), values);
}

/// <summary>Everything the mod puts on screen.</summary>
public static class Texts
{
    public static readonly Phrase Source = new("Neighbor Basket", "邻居篮子");

    public static readonly Phrase FillButton = new("Fill", "装篮");

    public static readonly Phrase FillTitle = new(
        "Put one cheap item for each thing she wants in the basket, from your home storage and your bag",
        "从家中储物和背包里，为她想要的每一类物品各放一件便宜的进篮子");

    public static readonly Phrase CallBackButton = new("Call back", "召回");

    public static readonly Phrase CallBackTitle = new("Bring the basket home now", "让篮子现在就回来");

    public static readonly Phrase Filled = new("Put {0} in the basket for: {1}.", "已放入 {0} 件，对应：{1}。");

    public static readonly Phrase FilledFood = new("Put {0} food in the basket.", "已放入 {0} 件食物。");

    public static readonly Phrase NoneFound = new("Nothing at home or in your bag for: {0}.", "家中和背包里没有：{0}。");

    public static readonly Phrase NotMoved = new("{0} could not be moved.", "{0} 件无法放入。");

    public static readonly Phrase NoNeighbor = new("Nobody is waiting on this basket.", "现在没有人在等这个篮子。");

    public static readonly Phrase Away = new("The basket is away.", "篮子还没回来。");

    public static readonly Phrase Pending = new("She has not said what she wants yet.", "她还没说想要什么。");

    public static readonly Phrase Full = new("The basket is full.", "篮子已经满了。");

    public static readonly Phrase AllThere = new("The basket already has everything she wants.", "她想要的都已经在篮子里了。");

    public static readonly Phrase NotOpen = new("Open the basket first.", "请先打开篮子。");

    public static readonly Phrase Food = new("food", "食物");

    public static readonly Phrase CalledBack = new("The basket is on its way home.", "篮子正在回来的路上。");

    public static readonly Phrase NotAway = new("The basket is already home.", "篮子已经在家了。");

    public static readonly Phrase Failed = new("Neighbor Basket failed, see BepInEx/neighbor-basket.log.", "邻居篮子出错，详见 BepInEx/neighbor-basket.log。");

    public static readonly Phrase TierDrop = new("About {0} h until her condition drops a step. Send the basket to hold it.", "约 {0} 小时后她的状态会下降一档。送出篮子可以保住。");

    public static readonly Phrase StruggleSoon = new("About {0} h until she starts to struggle. Send the basket to hold it.", "约 {0} 小时后她会陷入挣扎。送出篮子可以避免。");

    public static readonly Phrase CrisisDeadline = new("About {0} h left to help with her trouble. One item of the asked kind is enough.", "她遇到的麻烦还剩约 {0} 小时。送一件所需类别的物品就够了。");

    public static readonly Phrase Struggle = new("She is struggling. About {0} h left to send her food.", "她正在挣扎，还剩约 {0} 小时送食物给她。");

    public static readonly Phrase Dying = new("She is dying. Send her food now.", "她快不行了，马上送食物给她。");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        Source, FillButton, FillTitle, CallBackButton, CallBackTitle, Filled, FilledFood, NoneFound, NotMoved, NoNeighbor, Away, Pending, Full, AllThere, NotOpen, Food, CalledBack, NotAway, Failed,
        TierDrop, StruggleSoon, CrisisDeadline, Struggle, Dying,
    };

    public static string Warn(Warning warning, bool isChinese) => warning.Kind switch
    {
        WarningKind.TierDrop => TierDrop.Say(isChinese, warning.HoursLeft),
        WarningKind.StruggleSoon => StruggleSoon.Say(isChinese, warning.HoursLeft),
        WarningKind.CrisisDeadline => CrisisDeadline.Say(isChinese, warning.HoursLeft),
        WarningKind.Struggle => Struggle.Say(isChinese, warning.HoursLeft),
        _ => Dying.Pick(isChinese),
    };

    /// <summary>
    /// The toast after a Fill press. Category names come from the game's wish list; a category it did not name is
    /// shown as its number. <paramref name="failed"/> counts picks the game refused to move.
    /// </summary>
    public static string Outcome(FillResult result, int failed, IReadOnlyDictionary<int, string> names, bool isChinese)
    {
        switch (result.Stop)
        {
            case FillStop.NoNeighbor: return NoNeighbor.Pick(isChinese);
            case FillStop.Away: return Away.Pick(isChinese);
            case FillStop.Pending: return Pending.Pick(isChinese);
            case FillStop.Full: return Full.Pick(isChinese);
            case FillStop.AllThere: return AllThere.Pick(isChinese);
        }

        var parts = new List<string>();
        var moved = result.Picks.Count - failed;

        if (moved > 0)
        {
            parts.Add(result.IsFoodOnly
                ? FilledFood.Say(isChinese, moved)
                : Filled.Say(isChinese, moved, Join(result.Picks.Select(pick => pick.Item.Category).Distinct(), names, isChinese)));
        }

        if (failed > 0) parts.Add(NotMoved.Say(isChinese, failed));

        if (result.Missing.Count > 0) parts.Add(NoneFound.Say(isChinese, result.IsFoodOnly ? Food.Pick(isChinese) : Join(result.Missing, names, isChinese)));

        return parts.Count == 0 ? Full.Pick(isChinese) : string.Join(" ", parts);
    }

    private static string Join(IEnumerable<int> categories, IReadOnlyDictionary<int, string> names, bool isChinese) =>
        string.Join(isChinese ? "、" : ", ", categories.Select(category => names.TryGetValue(category, out var name) && !string.IsNullOrWhiteSpace(name) ? name : "#" + category));
}
