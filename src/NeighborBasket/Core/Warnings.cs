using System;
using System.Collections.Generic;

namespace NeighborBasket.Core;

/// <summary>
/// The neighbor's survival clock as the game keeps it. Seconds are the game clock's total seconds. A tier drops each
/// time TierDropHours pass from TierAnchorSeconds without a basket (a send moves the anchor); at a floor of 1 the drop
/// starts a struggle instead. StruggleState is 0 for none, 1 while she struggles (until StruggleDeadlineSeconds) and 2
/// while she is dying. A crisis fails at the start of CrisisDeadlineDay.
/// </summary>
public sealed record NeedState(
    bool IsActive,
    int Tier,
    int TierFloor,
    int TierAnchorSeconds,
    int TierDropHours,
    int StruggleState,
    int StruggleDeadlineSeconds,
    bool HasCrisis,
    int CrisisDeadlineDay,
    int NowSeconds,
    int Day,
    int Hour24);

public enum WarningKind
{
    TierDrop,
    StruggleSoon,
    CrisisDeadline,
    Struggle,
    Dying,
}

/// <summary>One warning; Key is unique to the event, so each is shown once.</summary>
public sealed record Warning(WarningKind Kind, int HoursLeft, string Key);

public static class Warnings
{
    private const int SecondsPerHour = 3600;

    /// <summary>The warnings due now that are not in <paramref name="shown"/>; each one returned is added to it.</summary>
    public static IReadOnlyList<Warning> Check(NeedState state, int warnHours, ISet<string> shown)
    {
        var due = new List<Warning>();

        if (!state.IsActive) return due;

        foreach (var warning in Due(state, warnHours))
        {
            if (shown.Add(warning.Key)) due.Add(warning);
        }

        return due;
    }

    private static IEnumerable<Warning> Due(NeedState state, int warnHours)
    {
        if (state.StruggleState == 1)
        {
            yield return new Warning(WarningKind.Struggle, HoursUntil(state.StruggleDeadlineSeconds, state.NowSeconds), "struggle:" + state.StruggleDeadlineSeconds);
        }
        else if (state.StruggleState == 2)
        {
            yield return new Warning(WarningKind.Dying, 0, "dying:" + state.StruggleDeadlineSeconds);
        }
        else if (DropKind(state) is { } kind && state.TierAnchorSeconds >= 0)
        {
            var dropAt = state.TierAnchorSeconds + state.TierDropHours * SecondsPerHour;

            if (dropAt - state.NowSeconds <= warnHours * SecondsPerHour) yield return new Warning(kind, HoursUntil(dropAt, state.NowSeconds), "drop:" + dropAt);
        }

        if (state.HasCrisis && state.CrisisDeadlineDay >= 0)
        {
            var hoursLeft = Math.Max(0, (state.CrisisDeadlineDay - state.Day) * 24 - state.Hour24);

            if (hoursLeft <= warnHours) yield return new Warning(WarningKind.CrisisDeadline, hoursLeft, "crisis:" + state.CrisisDeadlineDay);
        }
    }

    /// <summary>What the next missed drop does: lowers the tier, starts a struggle at a floor of 1, or nothing at a higher floor.</summary>
    private static WarningKind? DropKind(NeedState state)
    {
        if (state.TierDropHours <= 0 || state.Tier <= 0) return null;
        if (state.Tier > state.TierFloor) return WarningKind.TierDrop;

        return state.TierFloor <= 1 ? WarningKind.StruggleSoon : null;
    }

    /// <summary>Whole hours left, rounded up so "1 h" never means "already gone".</summary>
    private static int HoursUntil(int atSeconds, int nowSeconds) => Math.Max(0, (atSeconds - nowSeconds + SecondsPerHour - 1) / SecondsPerHour);
}
