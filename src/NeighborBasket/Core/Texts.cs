using System;
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
        "Put in enough from your home storage for a big jump in her condition, using what you have most of. Shift-click packs the basket as full as it goes",
        "从家中储物里放入足够让她状态大幅提升的物品，优先用你最多的东西。按住 Shift 单击会把篮子尽量装满");

    public static readonly Phrase CallBackButton = new("Call back", "召回");

    public static readonly Phrase CallBackTitle = new("Bring the basket home now", "让篮子现在就回来");

    public static readonly Phrase Filled = new("Put {0} in the basket, about +{1} progress.", "已放入 {0} 组，约 +{1} 进度。");

    public static readonly Phrase FilledFood = new("Put {0} food in the basket.", "已放入 {0} 件食物。");

    public static readonly Phrase NoneFound = new("Nothing in home storage for: {0}.", "家中储物里没有：{0}。");

    public static readonly Phrase NotMoved = new("{0} could not be moved.", "{0} 件无法放入。");

    public static readonly Phrase NoNeighbor = new("Nobody is waiting on this basket.", "现在没有人在等这个篮子。");

    public static readonly Phrase Away = new("The basket is away.", "篮子还没回来。");

    public static readonly Phrase Pending = new("She has not said what she wants yet.", "她还没说想要什么。");

    public static readonly Phrase Full = new("The basket is full.", "篮子已经满了。");

    public static readonly Phrase GoalMet = new("The basket already holds enough. Shift-click Fill to pack it full.", "篮子里的东西已经够了。按住 Shift 单击「装篮」可以装满。");

    public static readonly Phrase ProgressOne = new("+{0} progress", "+{0} 进度");

    public static readonly Phrase ProgressEach = new("+{0} progress for one, +{1} for all {2}", "放入一件 +{0} 进度，全部 {2} 件 +{1}");

    public static readonly Phrase NotOpen = new("Open the basket first.", "请先打开篮子。");

    public static readonly Phrase Food = new("food", "食物");

    public static readonly Phrase CalledBack = new("The basket is on its way home.", "篮子正在回来的路上。");

    public static readonly Phrase NotAway = new("The basket is already home.", "篮子已经在家了。");

    public static readonly Phrase Failed = new("Neighbor Basket failed, see BepInEx/neighbor-basket.log.", "邻居篮子出错，详见 BepInEx/neighbor-basket.log。");

    public static readonly Phrase TierDrop = new("About {0} h until her condition drops a step. Send the basket to hold it.", "约 {0} 小时后她的状态会下降一档。送出篮子可以保住。");

    public static readonly Phrase StruggleSoon = new("About {0} h until she starts to struggle. Send the basket to hold it.", "约 {0} 小时后她会陷入挣扎。送出篮子可以避免。");

    public static readonly Phrase CrisisDeadline = new("About {0} h left to help with her trouble. One item of the asked kind is enough.", "她遇到的麻烦还剩约 {0} 小时。送一件所需类别的物品就够了。");

    public static readonly Phrase Struggle = new("She is struggling. About {0} h left to send her food.", "她正在挣扎，还剩约 {0} 小时送食物给她。");

    public static readonly Phrase StruggleNeed = new(
        "She is struggling. About {0} h left to send her food: at least {1} satiety pulls her through, {2} or more for a better recovery.",
        "她正在挣扎，还剩约 {0} 小时送食物给她：至少 {1} 饱腹能让她撑过去，{2} 以上恢复得更好。");

    public static readonly Phrase Streak = new(
        "Send the basket today to keep the streak ({0} of {1} days). About {2} h left today.",
        "今天送出篮子才能保住连续赠送（{0}/{1} 天）。今天还剩约 {2} 小时。");

    public static readonly Phrase BasketSatiety = new("The basket holds {0} satiety; she needs at least {1}.", "篮子里共有 {0} 饱腹，她至少需要 {1}。");

    public static readonly Phrase PutAwayButton = new("To storage", "放进储物");

    public static readonly Phrase PutAwayTitle = new("Put everything in the basket into your home storage, the same way Storage Hub puts the bag away", "把篮子里的所有东西收进家中储物，和储物中心收纳背包的方式一样");

    public static readonly Phrase NoStorageHub = new("To storage needs Storage Hub.", "「放进储物」需要储物中心模组。");

    public static readonly Phrase TakeAllButton = new("To bag", "放进背包");

    public static readonly Phrase TakeAllTitle = new("Put everything in the basket into your bag, her gifts included", "把篮子里的所有东西放进背包，包括她的回礼");

    public static readonly Phrase TakeAllShort = new("{0} of {1} stacks did not fit in your bag.", "有 {0}/{1} 组放不进背包。");

    public static readonly Phrase BreakButton = new("Break down", "拆解");

    public static readonly Phrase BreakTitle = new("Take apart the gifts in the basket that break down by hand, the way the game's own Use does. They go to your bag first; your character does them one by one.", "把篮子里能手工拆开的回礼拆成材料，和游戏里的「使用」一样。它们先放进背包，再由角色逐件拆开。");

    public static readonly Phrase BreakStarted = new("Breaking down {0} by hand.", "正在手工拆解 {0} 件。");

    public static readonly Phrase BreakShort = new("{0} of {1} could not be broken down (bag full, or the game refused). They stay where they are.", "有 {0}/{1} 件无法拆解（背包已满或游戏不允许），留在原处。");

    public static readonly Phrase AppTile = new("Neighbor", "邻居");

    public static readonly Phrase AppTitle = new("Neighbor", "邻居");

    public static readonly Phrase AppBack = new("Back", "返回");

    public static readonly Phrase AppEmpty = new("Nothing logged yet. Sends, returns and her thoughts show here from now on.", "还没有记录。之后的送出、送回和她的心声都会显示在这里。");

    public static readonly Phrase AppNoRescue = new("Nobody to help right now.", "现在没有需要帮助的人。");

    public static readonly Phrase AppAffinity = new("Affinity", "好感");

    public static readonly Phrase AppAffinityValue = new("{0} (good ending at {1})", "{0}（达到 {1} 为好结局）");

    public static readonly Phrase AppProgress = new("Progress", "进度");

    public static readonly Phrase AppStreak = new("Send streak", "连续赠送");

    public static readonly Phrase AppStrugglesLeft = new("Struggles left", "剩余挣扎次数");

    public static readonly Phrase AppDay = new("Day {0}", "第 {0} 天");

    public static readonly Phrase AppSent = new("Sent", "送出");

    public static readonly Phrase AppReturned = new("Came back", "送回");

    public static readonly Phrase AppSaid = new("She thought", "她的心声");

    public static readonly Phrase AppGained = new("{0} progress, {1} affinity", "进度 {0}，好感 {1}");

    public static readonly Phrase AppNothing = new("(empty)", "（空）");

    public static readonly Phrase Dying = new("She is dying. Send her food now.", "她快不行了，马上送食物给她。");

    public static readonly Phrase CardCondition = new("Condition", "状态");

    public static readonly Phrase CardTier = new("Tier {0}", "第 {0} 档");

    public static readonly Phrase CardDrops = new("drops in {0} h", "{0} 小时后下降");

    public static readonly Phrase CardStruggleIn = new("struggles in {0} h", "{0} 小时后陷入挣扎");

    public static readonly Phrase CardStruggling = new("Struggling, {0} h left", "挣扎中，还剩约 {0} 小时");

    public static readonly Phrase CardDying = new("Dying", "濒死");

    public static readonly Phrase CardSatiety = new("Satiety needed", "所需饱腹");

    public static readonly Phrase CardSatietyValue = new("at least {0}, better from {1}", "至少 {0}，{1} 以上恢复更好");

    public static readonly Phrase CardTrouble = new("Trouble", "麻烦");

    public static readonly Phrase CardTroubleOf = new("Trouble: {0}", "麻烦：{0}");

    public static readonly Phrase CardDue = new("due in {0} h", "剩约 {0} 小时");

    public static readonly Phrase CardStreakToday = new("{0} / {1} days, send today", "{0} / {1} 天，今天还没送");

    public static readonly Phrase CardStreakSent = new("{0} / {1} days, sent today", "{0} / {1} 天，今天已送");

    public static readonly Phrase CardProgressValue = new("{0} / {1}, affinity {2} (good ending at {3})", "{0} / {1}，好感 {2}（达到 {3} 为好结局）");

    public static readonly Phrase CardFills = new("Fill adds {0} stacks from storage, about +{1} progress.", "装篮会从储物放入 {0} 组，约 +{1} 进度。");

    public static readonly Phrase CardFillsFood = new("Fill adds {0} food from storage.", "装篮会从储物放入 {0} 组食物。");

    public static readonly Phrase CardFillsNothing = new("Fill finds nothing at hand to add.", "装篮在手边找不到可放的东西。");

    public static readonly Phrase StationButton = new("Basket station", "篮子工作台");

    public static readonly Phrase GameScreen = new("Game screen", "游戏界面");

    public static readonly Phrase TakeFrom = new("Take from", "取自");

    public static readonly Phrase TakeFromSub = new("your home storage and your bag", "家中储物和背包");

    public static readonly Phrase AllStorage = new("All storage", "全部储物");

    public static readonly Phrase StorageGroup = new("Storage", "储物");

    public static readonly Phrase OnYou = new("On you", "随身");

    public static readonly Phrase YourBag = new("Bag", "背包");

    public static readonly Phrase SheWants = new("She wants", "她想要");

    public static readonly Phrase Everything = new("Everything", "全部");

    public static readonly Phrase Search = new("Search items", "搜索物品");

    public static readonly Phrase WantedTag = new("WANTED", "想要");

    public static readonly Phrase NothingHere = new("Nothing here.", "这里没有东西。");

    public static readonly Phrase NothingWanted = new("Nothing here she wants. Try Everything.", "这里没有她想要的，试试「全部」。");

    public static readonly Phrase AddHint = new("Click puts one in the basket. Shift-click puts the whole stack.", "单击放入一件，按住 Shift 单击放入整组。");

    public static readonly Phrase BasketTitle = new("Basket", "篮子");

    public static readonly Phrase Cells = new("{0} / {1} cells", "{0} / {1} 格");

    public static readonly Phrase TakeHint = new("Click an item in the basket to take it back.", "单击篮子里的物品可以取回。");

    public static readonly Phrase WeightLabel = new("Weight", "重量");

    public static readonly Phrase KgFree = new("{0} kg free", "剩余 {0} 千克");

    public static readonly Phrase IfSent = new("+{0} if sent now", "现在送出 +{0}");

    public static readonly Phrase SendButton = new("Send basket", "送出篮子");

    public static readonly Phrase NextDoor = new("Next door", "隔壁");

    public static readonly Phrase CannotTake = new("She can't take this.", "她不收这个。");

    public static readonly Phrase AddFailed = new("{0} does not fit in the basket.", "{0} 放不进篮子。");

    public static readonly Phrase TakeFailed = new("{0} could not be taken back.", "{0} 无法取回。");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        Source, FillButton, FillTitle, CallBackButton, CallBackTitle, Filled, FilledFood, NoneFound, NotMoved, NoNeighbor, Away, Pending, Full, GoalMet, ProgressOne, ProgressEach, NotOpen, Food, CalledBack, NotAway, Failed,
        TierDrop, StruggleSoon, CrisisDeadline, Struggle, StruggleNeed, Streak, BasketSatiety, PutAwayButton, PutAwayTitle, NoStorageHub, TakeAllButton, TakeAllTitle, TakeAllShort, BreakButton, BreakTitle, BreakStarted, BreakShort, Dying,
        AppTile, AppTitle, AppBack, AppEmpty, AppNoRescue, AppAffinity, AppAffinityValue, AppProgress, AppStreak, AppStrugglesLeft, AppDay, AppSent, AppReturned, AppSaid, AppGained, AppNothing,
        CardCondition, CardTier, CardDrops, CardStruggleIn, CardStruggling, CardDying, CardSatiety, CardSatietyValue, CardTrouble, CardTroubleOf, CardDue, CardStreakToday, CardStreakSent,
        CardProgressValue, CardFills, CardFillsFood, CardFillsNothing,
        StationButton, GameScreen, TakeFrom, TakeFromSub, AllStorage, StorageGroup, OnYou, YourBag, SheWants, Everything, Search, WantedTag, NothingHere, NothingWanted, AddHint,
        BasketTitle, Cells, TakeHint, WeightLabel, KgFree, IfSent, SendButton, NextDoor, CannotTake, AddFailed, TakeFailed,
    };

    public static string Warn(Warning warning, bool isChinese) => warning.Kind switch
    {
        WarningKind.TierDrop => TierDrop.Say(isChinese, warning.HoursLeft),
        WarningKind.StruggleSoon => StruggleSoon.Say(isChinese, warning.HoursLeft),
        WarningKind.CrisisDeadline => CrisisDeadline.Say(isChinese, warning.HoursLeft),
        WarningKind.Struggle when warning.A > 0 => StruggleNeed.Say(isChinese, warning.HoursLeft, warning.A, Math.Max(warning.A, warning.B)),
        WarningKind.Struggle => Struggle.Say(isChinese, warning.HoursLeft),
        WarningKind.Streak => Streak.Say(isChinese, warning.A, warning.B, warning.HoursLeft),
        _ => Dying.Pick(isChinese),
    };

    /// <summary>
    /// The toast after a Fill press. Category names come from the game's wish list; a category it did not name is
    /// shown as its number. <paramref name="failed"/> counts picks the game refused to move; the progress shown is the plan's, so a refused pick still counts in it.
    /// </summary>
    public static string Outcome(FillResult result, int failed, IReadOnlyDictionary<int, string> names, bool isChinese)
    {
        switch (result.Stop)
        {
            case FillStop.NoNeighbor: return NoNeighbor.Pick(isChinese);
            case FillStop.Away: return Away.Pick(isChinese);
            case FillStop.Pending: return Pending.Pick(isChinese);
            case FillStop.Full: return Full.Pick(isChinese);
            case FillStop.GoalMet: return GoalMet.Pick(isChinese);
        }

        var parts = new List<string>();
        var moved = result.Picks.Count - failed;

        if (moved > 0)
        {
            parts.Add(result.IsFoodOnly
                ? FilledFood.Say(isChinese, moved)
                : Filled.Say(isChinese, moved, result.Gain));
        }

        if (failed > 0) parts.Add(NotMoved.Say(isChinese, failed));

        if (result.Missing.Count > 0) parts.Add(NoneFound.Say(isChinese, result.IsFoodOnly ? Food.Pick(isChinese) : Join(result.Missing, names, isChinese)));

        return parts.Count == 0 ? Full.Pick(isChinese) : string.Join(" ", parts);
    }

    private static string Join(IEnumerable<int> categories, IReadOnlyDictionary<int, string> names, bool isChinese) =>
        string.Join(isChinese ? "、" : ", ", categories.Select(category => names.TryGetValue(category, out var name) && !string.IsNullOrWhiteSpace(name) ? name : "#" + category));
}
