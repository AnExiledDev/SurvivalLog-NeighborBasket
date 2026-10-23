using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NeighborBasket.Core;

/// <summary>
/// Builds the data for the card above the basket panel: her condition and when it next drops, her trouble's deadline,
/// the send streak, the rescue's progress, the satiety a struggle needs, and what Fill would add right now. A row's
/// tone is "warn" when its deadline is within the warning hours and "bad" when she is struggling, dying, or her trouble
/// is that close; the page colours them. Weight goes out in the game's units with the free weight beside it, so the
/// page can turn it into the kg the basket panel shows.
/// </summary>
public static class CardView
{
    public static string Json(NeedState? need, Tracker? tracker, string? troubleName, FillResult? plan, float freeWeight, int warnHours, bool isChinese)
    {
        var json = new StringBuilder("{");

        json.Append("\"title\":").Append(JsText.Literal(Texts.AppTitle.Pick(isChinese)));
        json.Append(",\"day\":").Append(JsText.Literal(need is { IsActive: true } ? Texts.AppDay.Say(isChinese, need.Day) : ""));
        json.Append(",\"rows\":[");
        json.Append(string.Join(",", Rows(need, tracker, troubleName, warnHours, isChinese).Select(Row)));
        json.Append("],\"plan\":").Append(JsText.Literal(plan == null ? "" : PlanText(plan, isChinese)));

        var weight = plan == null || plan.Picks.Count == 0 || freeWeight <= 0 || float.IsInfinity(freeWeight) ? 0 : plan.Picks.Sum(pick => pick.Weight);

        json.Append(",\"weight\":").Append(weight.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"free\":").Append((weight > 0 ? freeWeight : 0).ToString("0.###", CultureInfo.InvariantCulture));
        json.Append(",\"weightText\":").Append(JsText.Literal(Texts.CardWeight.Pick(isChinese)));

        return json.Append('}').ToString();
    }

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

    /// <summary>What a Fill press would do now: how many wished and extra items, or why it would add nothing.</summary>
    public static string PlanText(FillResult plan, bool isChinese)
    {
        switch (plan.Stop)
        {
            case FillStop.NoNeighbor: return Texts.NoNeighbor.Pick(isChinese);
            case FillStop.Away: return Texts.Away.Pick(isChinese);
            case FillStop.Pending: return Texts.Pending.Pick(isChinese);
            case FillStop.Full: return Texts.Full.Pick(isChinese);
            case FillStop.AllThere: return Texts.AllThere.Pick(isChinese);
        }

        if (plan.Picks.Count == 0) return Texts.CardFillsNothing.Pick(isChinese);

        if (plan.IsFoodOnly) return Texts.CardFillsFood.Say(isChinese, plan.Picks.Count);

        return Texts.CardFills.Say(isChinese, plan.Picks.Count - plan.Extras, plan.Extras);
    }

    private static string Row(CardRow row) =>
        "{\"label\":" + JsText.Literal(row.Label) + ",\"value\":" + JsText.Literal(row.Value) + ",\"tone\":" + JsText.Literal(row.Tone) + ",\"tier\":" + JsText.Literal(row.Tier) + "}";
}
