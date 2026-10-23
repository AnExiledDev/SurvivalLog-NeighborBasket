using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using NeighborBasket.Core;
using Il2CppItems = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.Battle.Logic.ItemData>;

namespace NeighborBasket.Adapters;

/// <summary>The running line's survival clock and its standing, read together.</summary>
internal sealed record LineRead(NeedState Need, Tracker Tracker, int Progress, int Affinity);

/// <summary>
/// Reads whichever neighbor line is running: the girl's (NeighborRescueManager) or the boy's (GirlCompanionManager).
/// Both keep the same fields and rules, under their own global settings ("Neighbor_" for her, "Companion_" for him);
/// only one has a rescue going at a time.
/// </summary>
internal static class NeedReader
{
    private const string GirlPrefix = "Neighbor_";
    private const string BoyPrefix = "Companion_";

    public static LineRead? Read()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var time = world?._GameTimeManager;

        if (world == null || time == null) return null;

        var now = time.GetTotalSeconds();
        var day = time.GetDay();
        var hour = time.GetHour24();

        if (world._NeighborRescueManager is { } girl && girl.GmGetActiveRescueId() > 0 && girl.IsRescueActive())
        {
            var need = new NeedState(true, girl.GmGetSurvivalTier(), girl.GetTierFloor(), girl.GmGetTierAnchorSeconds(), Setting(GirlPrefix, "TierDropHours", 48), girl.GmGetStruggleState(),
                girl.GmGetStruggleDeadlineSeconds(), girl.GmGetActiveCrisisIndex() >= 0, girl.crisisDeadlineDay, now, day, hour,
                girl.GmGetLastSendDay(), girl.GmGetConsecutiveSendDays(), Setting(GirlPrefix, "StreakDays", 3), Setting(GirlPrefix, "StruggleSatietyMin", 10), Setting(GirlPrefix, "StruggleSatietyHigh", 100));

            return new LineRead(need, TrackerOf(GirlPrefix, need, girl.GmGetAffinity(), girl.GetLineProgressTotal(), girl.GetLineProgressMax(), girl.GmGetStruggleCount()), girl.GetLineProgressTotal(), girl.GmGetAffinity());
        }

        if (world._GirlCompanionManager is { } boy && boy.GmGetActiveRescueId() > 0 && boy.IsRescueActive())
        {
            var need = new NeedState(true, boy.GmGetSurvivalTier(), boy.GetTierFloor(), boy.GmGetTierAnchorSeconds(), Setting(BoyPrefix, "TierDropHours", 48), boy.GmGetStruggleState(),
                boy.GmGetStruggleDeadlineSeconds(), boy.GmGetActiveCrisisIndex() >= 0, boy.crisisDeadlineDay, now, day, hour,
                boy.GmGetLastSendDay(), boy.GmGetConsecutiveSendDays(), Setting(BoyPrefix, "StreakDays", 3), Setting(BoyPrefix, "StruggleSatietyMin", 10), Setting(BoyPrefix, "StruggleSatietyHigh", 100));

            return new LineRead(need, TrackerOf(BoyPrefix, need, boy.GmGetAffinity(), boy.GetLineProgressTotal(), boy.GetLineProgressMax(), boy.GmGetStruggleCount()), boy.GetLineProgressTotal(), boy.GmGetAffinity());
        }

        return null;
    }

    /// <summary>The satiety the running line's own rescue rule counts for these items; null with no rescue going.</summary>
    public static float? Satiety(Il2CppItems items)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();

        if (world?._NeighborRescueManager is { } girl && girl.GmGetActiveRescueId() > 0 && girl.IsRescueActive()) return girl.CalcBasketSatiety(items);
        if (world?._GirlCompanionManager is { } boy && boy.GmGetActiveRescueId() > 0 && boy.IsRescueActive()) return boy.CalcBasketSatiety(items);

        return null;
    }

    private static Tracker TrackerOf(string prefix, NeedState need, int affinity, int progress, int progressMax, int struggles) =>
        new(affinity, Setting(prefix, "AffinityThresholdEnding", 50), progress, progressMax, need.ConsecutiveSendDays, need.StreakDays, Setting(prefix, "StruggleMaxCount", 3) - struggles);

    /// <summary>The game's own global setting, read the way its rescue code reads it, with the same fallback.</summary>
    private static int Setting(string prefix, string name, int fallback) =>
        GameSingleton.Existing<ConfigManager>()?.Get_Config_GlobalSetting(prefix + name) is { } row ? row.Params1 : fallback;
}
