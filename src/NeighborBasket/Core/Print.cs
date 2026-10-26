using System;
using System.Collections.Generic;

namespace NeighborBasket.Core;

/// <summary>
/// A fingerprint of what a view is drawn from, built without allocating, so an unchanged view is not built again. Each
/// value is folded in with Mix; Start is the empty print.
/// </summary>
public static class Print
{
    public const ulong Start = 14695981039346656037UL;

    public static ulong Mix(ulong print, long value)
    {
        print = (print ^ (ulong)value) * 0x9E3779B97F4A7C15UL;

        return print ^ (print >> 32);
    }

    public static ulong Mix(ulong print, bool value) => Mix(print, value ? 1L : 0L);

    public static ulong Mix(ulong print, float value) => Mix(print, BitConverter.SingleToInt32Bits(value));

    public static ulong Mix(ulong print, string? text)
    {
        if (text == null) return Mix(print, -1L);

        print = Mix(print, text.Length);

        for (var i = 0; i < text.Length; i++) print = Mix(print, text[i]);

        return print;
    }

    /// <summary>
    /// Everything her status rows and warnings show: every field but the clock, which goes in as the whole hours the
    /// rows show, so a print holds still between two reads within the same shown hour.
    /// </summary>
    public static ulong Line(NeedState? need, Tracker? tracker)
    {
        var print = Start;

        if (need is { } n)
        {
            print = Mix(print, n.IsActive);
            print = Mix(print, n.Tier);
            print = Mix(print, n.TierFloor);
            print = Mix(print, n.TierAnchorSeconds);
            print = Mix(print, n.TierDropHours);
            print = Mix(print, n.StruggleState);
            print = Mix(print, n.StruggleDeadlineSeconds);
            print = Mix(print, n.HasCrisis);
            print = Mix(print, n.CrisisDeadlineDay);
            print = Mix(print, n.Day);
            print = Mix(print, n.Hour24);
            print = Mix(print, n.LastSendDay);
            print = Mix(print, n.ConsecutiveSendDays);
            print = Mix(print, n.StreakDays);
            print = Mix(print, n.SatietyMin);
            print = Mix(print, n.SatietyHigh);
            print = Mix(print, Warnings.HoursUntil(n.TierAnchorSeconds + n.TierDropHours * 3600, n.NowSeconds));
            print = Mix(print, Warnings.HoursUntil(n.StruggleDeadlineSeconds, n.NowSeconds));
        }
        else
        {
            print = Mix(print, -2L);
        }

        if (tracker is { } t)
        {
            print = Mix(print, t.Affinity);
            print = Mix(print, t.EndingAt);
            print = Mix(print, t.Progress);
            print = Mix(print, t.ProgressMax);
            print = Mix(print, t.Streak);
            print = Mix(print, t.StreakDays);
            print = Mix(print, t.StrugglesLeft);
        }
        else
        {
            print = Mix(print, -3L);
        }

        return print;
    }

    /// <summary>Her wish as the basket screen shows it, with the names it gives each category.</summary>
    public static ulong Wish(BasketWish wish, IReadOnlyDictionary<int, string>? names)
    {
        var print = Start;

        print = Mix(print, wish.IsActive);
        print = Mix(print, wish.IsPending);
        print = Mix(print, wish.IsAway);
        print = Mix(print, wish.IsDying);
        print = Mix(print, wish.CrisisCategory ?? -1);
        print = Mix(print, wish.StageLeft);
        print = Mix(print, wish.Categories.Count);

        for (var i = 0; i < wish.Categories.Count; i++) print = Mix(print, wish.Categories[i]);

        if (names == null) return print;

        print = Mix(print, names.Count);

        foreach (var name in names)
        {
            print = Mix(print, name.Key);
            print = Mix(print, name.Value);
        }

        return print;
    }
}
