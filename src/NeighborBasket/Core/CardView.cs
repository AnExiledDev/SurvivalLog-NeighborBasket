using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NeighborBasket.Core;

/// <summary>
/// Her status rows for the basket station and the phone: her condition and when it next drops, her trouble's deadline,
/// the send streak, the rescue's progress and the satiety a struggle needs, plus what Fill would add right now. A row's
/// tone is "warn" when its deadline is within the warning hours and "bad" when she is struggling, dying, or her trouble
/// is that close; the page colours them.
/// </summary>
public static class CardView
{
    /// <summary>One card row; Tier is set on the condition row so the page can show the game's own name for the tier.</summary>
    public sealed record CardRow(string Label, string Value, string Tone = "", string Tier = "");

    public static IEnumerable<CardRow> Rows(NeedState? need, Tracker? tracker, string? troubleName, int warnHours, bool isChinese)
    {
        if (need is not { IsActive: true } state)
        {
            yield return new CardRow(Texts.AppNoRescue.Pick(isChinese), "");

            yield break;
        }

        yield return Condition(state, warnHours, isChinese);

        if (state.StruggleState == 1 && state.SatietyMin > 0)
        {
            yield return new CardRow(Texts.CardSatiety.Pick(isChinese), Texts.CardSatietyValue.Say(isChinese, state.SatietyMin, Math.Max(state.SatietyMin, state.SatietyHigh)), "warn");
        }

        if (state.HasCrisis && state.CrisisDeadlineDay >= 0)
        {
            var hours = Warnings.HoursToCrisis(state);
            var label = string.IsNullOrWhiteSpace(troubleName) ? Texts.CardTrouble.Pick(isChinese) : Texts.CardTroubleOf.Say(isChinese, troubleName!);

            yield return new CardRow(label, Texts.CardDue.Say(isChinese, hours), hours <= warnHours ? "bad" : "warn");
        }

        if (state.StreakDays > 0)
        {
            // A day skipped breaks the streak; the game only resets its count at the next send.
            var count = state.LastSendDay >= 0 && state.LastSendDay >= state.Day - 1 ? state.ConsecutiveSendDays : 0;
            var isSentToday = state.LastSendDay == state.Day;
            var value = (isSentToday ? Texts.CardStreakSent : Texts.CardStreakToday).Say(isChinese, count, state.StreakDays);

            yield return new CardRow(Texts.AppStreak.Pick(isChinese), value, !isSentToday && count >= 1 ? "warn" : "");
        }

        if (tracker is { } t) yield return new CardRow(Texts.AppProgress.Pick(isChinese), Texts.CardProgressValue.Say(isChinese, t.Progress, t.ProgressMax, t.Affinity, t.EndingAt));
    }

    private static CardRow Condition(NeedState state, int warnHours, bool isChinese)
    {
        var label = Texts.CardCondition.Pick(isChinese);

        if (state.StruggleState >= 2) return new CardRow(label, Texts.CardDying.Pick(isChinese), "bad");

        if (state.StruggleState == 1) return new CardRow(label, Texts.CardStruggling.Say(isChinese, Warnings.HoursUntil(state.StruggleDeadlineSeconds, state.NowSeconds)), "bad");

        var tier = Texts.CardTier.Say(isChinese, state.Tier);
        var drop = Warnings.DropKind(state);

        if (drop == null) return new CardRow(label, "", "", tier);

        var hours = Warnings.HoursToDrop(state);
        var value = (drop == WarningKind.TierDrop ? Texts.CardDrops : Texts.CardStruggleIn).Say(isChinese, hours);

        return new CardRow(label, value, hours <= warnHours ? "warn" : "", tier);
    }

    /// <summary>What a Fill press would do now: how many stacks and the progress they add, or why it would add nothing.</summary>
    public static string PlanText(FillResult plan, bool isChinese)
    {
        switch (plan.Stop)
        {
            case FillStop.NoNeighbor: return Texts.NoNeighbor.Pick(isChinese);
            case FillStop.Away: return Texts.Away.Pick(isChinese);
            case FillStop.Pending: return Texts.Pending.Pick(isChinese);
            case FillStop.Full: return Texts.Full.Pick(isChinese);
            case FillStop.GoalMet: return Texts.GoalMet.Pick(isChinese);
        }

        if (plan.Picks.Count == 0) return Texts.CardFillsNothing.Pick(isChinese);

        if (plan.IsFoodOnly) return Texts.CardFillsFood.Say(isChinese, plan.Picks.Count);

        return Texts.CardFills.Say(isChinese, plan.Picks.Count, plan.Gain);
    }

    internal static string RowJson(CardRow row) =>
        "{\"label\":" + JsText.Literal(row.Label) + ",\"value\":" + JsText.Literal(row.Value) + ",\"tone\":" + JsText.Literal(row.Tone) + ",\"tier\":" + JsText.Literal(row.Tier) + "}";
}
