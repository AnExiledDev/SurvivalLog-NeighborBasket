using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// Reads the survival clock of whichever neighbor line is running: the girl's (NeighborRescueManager) or the boy's
/// (GirlCompanionManager). Both keep the same fields and rules; only one has a rescue going at a time.
/// </summary>
internal static class NeedReader
{
    private const int DefaultTierDropHours = 48;

    public static NeedState? Read()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var time = world?._GameTimeManager;

        if (world == null || time == null) return null;

        var dropHours = TierDropHours();
        var now = time.GetTotalSeconds();
        var day = time.GetDay();
        var hour = time.GetHour24();

        if (world._NeighborRescueManager is { } girl && girl.GmGetActiveRescueId() > 0 && girl.IsRescueActive())
        {
            return new NeedState(true, girl.GmGetSurvivalTier(), girl.GetTierFloor(), girl.GmGetTierAnchorSeconds(), dropHours, girl.GmGetStruggleState(), girl.GmGetStruggleDeadlineSeconds(),
                girl.GmGetActiveCrisisIndex() >= 0, girl.crisisDeadlineDay, now, day, hour);
        }

        if (world._GirlCompanionManager is { } boy && boy.GmGetActiveRescueId() > 0 && boy.IsRescueActive())
        {
            return new NeedState(true, boy.GmGetSurvivalTier(), boy.GetTierFloor(), boy.GmGetTierAnchorSeconds(), dropHours, boy.GmGetStruggleState(), boy.GmGetStruggleDeadlineSeconds(),
                boy.GmGetActiveCrisisIndex() >= 0, boy.crisisDeadlineDay, now, day, hour);
        }

        return null;
    }

    /// <summary>The game's own setting, read the way its survival tick reads it.</summary>
    private static int TierDropHours() =>
        GameSingleton.Existing<ConfigManager>()?.Get_Config_GlobalSetting("Neighbor_TierDropHours") is { } row ? row.Params1 : DefaultTierDropHours;
}
