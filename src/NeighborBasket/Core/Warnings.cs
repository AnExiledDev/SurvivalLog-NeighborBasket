using System;
using System.Collections.Generic;

namespace NeighborBasket.Core;

/// <summary>
/// The neighbor's survival clock as the game keeps it. Seconds are the game clock's total seconds. A tier drops each
/// time TierDropHours pass from TierAnchorSeconds without a basket (a send moves the anchor); at a floor of 1 the drop
/// starts a struggle instead. StruggleState is 0 for none, 1 while she struggles (until StruggleDeadlineSeconds) and 2
/// while she is dying. A crisis fails at the start of CrisisDeadlineDay. The send streak counts days in a row with a
/// send (LastSendDay is the last day one went out); it pays at StreakDays and breaks when a day is skipped. A struggle
/// is pulled through by a basket with at least SatietyMin satiety, and recovers better from SatietyHigh.
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
    int Hour24,
    int LastSendDay = -1,
    int ConsecutiveSendDays = 0,
    int StreakDays = 0,
    int SatietyMin = 0,
    int SatietyHigh = 0);

public enum WarningKind
{
    TierDrop,
    StruggleSoon,
    CrisisDeadline,
    Struggle,
    Dying,
    Streak,
}

/// <summary>
/// One warning; Key is unique to the event, so each is shown once. A and B carry the numbers the text needs: the
/// satiety a struggle needs (least, and for the better recovery), or the streak so far and the days it pays at.
/// </summary>
public sealed record Warning(WarningKind Kind, int HoursLeft, string Key, int A = 0, int B = 0);

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

    /// <summary>Every warning due now, shown or not, the most urgent first: what she needs now, then the nearest deadline.</summary>
    public static IReadOnlyList<Warning> Active(NeedState state, int warnHours)
    {
        if (!state.IsActive) return Array.Empty<Warning>();

        var due = new List<Warning>(Due(state, warnHours));

        due.Sort((a, b) => Rank(a.Kind) != Rank(b.Kind) ? Rank(a.Kind).CompareTo(Rank(b.Kind)) : a.HoursLeft.CompareTo(b.HoursLeft));

        return due;
    }

    // Dying and struggling need food now; a crisis fails outright, a struggle about to start costs more than a tier,
    // and a streak only costs its reward.
    private static int Rank(WarningKind kind) => kind switch
    {
        WarningKind.Dying => 0,
        WarningKind.Struggle => 1,
        WarningKind.CrisisDeadline => 2,
        WarningKind.StruggleSoon => 3,
        WarningKind.TierDrop => 4,
        _ => 5,
    };

    /// <summary>Red when her life is at stake (struggling, dying, or about to struggle); amber for the rest.</summary>
    public static bool IsSevere(WarningKind kind) => kind is WarningKind.Dying or WarningKind.Struggle or WarningKind.StruggleSoon;

    private static IEnumerable<Warning> Due(NeedState state, int warnHours)
    {
        if (state.StruggleState == 1)
        {
            yield return new Warning(WarningKind.Struggle, HoursUntil(state.StruggleDeadlineSeconds, state.NowSeconds), "struggle:" + state.StruggleDeadlineSeconds, state.SatietyMin, state.SatietyHigh);
        }
        else if (state.StruggleState == 2)
        {
            yield return new Warning(WarningKind.Dying, 0, "dying:" + state.StruggleDeadlineSeconds);
        }
        else if (DropKind(state) is { } kind)
        {
            var dropAt = DropAt(state);

            if (dropAt - state.NowSeconds <= warnHours * SecondsPerHour) yield return new Warning(kind, HoursUntil(dropAt, state.NowSeconds), "drop:" + dropAt);
        }

        if (state.HasCrisis && state.CrisisDeadlineDay >= 0)
        {
            var hoursLeft = HoursToCrisis(state);

            if (hoursLeft <= warnHours) yield return new Warning(WarningKind.CrisisDeadline, hoursLeft, "crisis:" + state.CrisisDeadlineDay);
        }

        if (StreakAtRisk(state))
        {
            var hoursLeft = Math.Max(0, 24 - state.Hour24);

            if (hoursLeft <= warnHours) yield return new Warning(WarningKind.Streak, hoursLeft, "streak:" + state.Day, state.ConsecutiveSendDays, state.StreakDays);
        }
    }

    /// <summary>A streak is running, yesterday's send kept it, and nothing has gone out today, so it breaks at midnight.</summary>
    private static bool StreakAtRisk(NeedState state) =>
        state.StreakDays > 0 && state.ConsecutiveSendDays >= 1 && state.LastSendDay >= 0 && state.LastSendDay == state.Day - 1;

    /// <summary>What the next missed drop does: lowers the tier, starts a struggle at a floor of 1, or nothing at a higher floor.</summary>
    internal static WarningKind? DropKind(NeedState state)
    {
        if (state.TierDropHours <= 0 || state.Tier <= 0 || state.TierAnchorSeconds < 0) return null;
        if (state.Tier > state.TierFloor) return WarningKind.TierDrop;

        return state.TierFloor <= 1 ? WarningKind.StruggleSoon : null;
    }

    private static int DropAt(NeedState state) => state.TierAnchorSeconds + state.TierDropHours * SecondsPerHour;

    /// <summary>Whole hours until the next drop; only meaningful when DropKind is not null.</summary>
    internal static int HoursToDrop(NeedState state) => HoursUntil(DropAt(state), state.NowSeconds);

    /// <summary>Whole hours until her trouble fails, at the start of its deadline day.</summary>
    internal static int HoursToCrisis(NeedState state) => Math.Max(0, (state.CrisisDeadlineDay - state.Day) * 24 - state.Hour24);

    /// <summary>Whole hours left, rounded up so "1 h" never means "already gone".</summary>
    internal static int HoursUntil(int atSeconds, int nowSeconds) => Math.Max(0, (atSeconds - nowSeconds + SecondsPerHour - 1) / SecondsPerHour);
}
