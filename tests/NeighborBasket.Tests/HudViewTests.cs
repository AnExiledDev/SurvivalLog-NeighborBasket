using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class HudViewTests
{
    private const int Hour = 3600;

    // Day 1, 16:00 (hour 40). Tier 4 over a floor of 2, dropping at hour 46; her trouble fails at the start of day 2.
    private static NeedState State() => new(true, 4, 2, 0, 46, 0, 0, true, 2, 40 * Hour, 1, 16);

    [Fact]
    public void The_most_urgent_warning_comes_first_even_when_another_is_sooner()
    {
        // The tier drops in 6 h and the trouble fails in 8 h, but a failed trouble costs more, so it leads.
        var active = Warnings.Active(State(), 12);

        Assert.Equal(new[] { WarningKind.CrisisDeadline, WarningKind.TierDrop }, active.Select(w => w.Kind));
        Assert.Equal(new[] { 8, 6 }, active.Select(w => w.HoursLeft));
    }

    [Fact]
    public void A_struggle_leads_and_a_streak_comes_last()
    {
        // Struggling until hour 45, with a streak at risk at midnight (8 h) alongside the trouble (8 h).
        var state = State() with { StruggleState = 1, StruggleDeadlineSeconds = 45 * Hour, StreakDays = 5, ConsecutiveSendDays = 2, LastSendDay = 0 };

        Assert.Equal(new[] { WarningKind.Struggle, WarningKind.CrisisDeadline, WarningKind.Streak }, Warnings.Active(state, 12).Select(w => w.Kind));
    }

    [Fact]
    public void The_line_keeps_a_warning_up_after_its_toast_was_shown()
    {
        var shown = new HashSet<string>();

        Assert.Equal(2, Warnings.Check(State(), 12, shown).Count);
        Assert.Empty(Warnings.Check(State(), 12, shown));
        Assert.Equal(2, Warnings.Active(State(), 12).Count);
    }

    [Fact]
    public void Nothing_due_or_no_rescue_takes_the_line_away()
    {
        Assert.Empty(Warnings.Active(State() with { IsActive = false }, 12));
        Assert.Equal("null", HudView.Json(Warnings.Active(State() with { HasCrisis = false }, 4), false));
    }

    [Fact]
    public void The_line_shows_the_first_in_short_counts_the_rest_and_lists_all_in_full()
    {
        var json = HudView.Json(Warnings.Active(State(), 12), false);

        Assert.StartsWith("{\"text\":\"Trouble runs out in 8 h\",\"tone\":\"warn\",\"more\":\"+1 more\"", json);
        Assert.Contains("\"all\":[{\"text\":\"About 8 h left to help with her trouble.", json);
        Assert.Contains("{\"text\":\"About 6 h until her condition drops a step.", json);
    }

    [Fact]
    public void Her_life_at_stake_is_red_and_a_lone_warning_has_no_more()
    {
        var state = State() with { StruggleState = 2, HasCrisis = false };

        Assert.Equal("{\"text\":\"Dying: send food now\",\"tone\":\"bad\",\"more\":\"\",\"title\":\"Neighbor alerts\",\"all\":[{\"text\":\"She is dying. Send her food now.\",\"tone\":\"bad\"}]}", HudView.Json(Warnings.Active(state, 12), false));
        Assert.Contains("\"text\":\"快不行了：马上送食物\"", HudView.Json(Warnings.Active(state, 12), true));
    }
}
