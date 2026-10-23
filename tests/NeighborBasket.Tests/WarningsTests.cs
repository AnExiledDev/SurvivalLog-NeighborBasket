using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class WarningsTests
{
    private const int Hour = 3600;

    // Tier 4 over a floor of 2, anchored at 0, dropping every 48 h; no struggle and no crisis.
    private static NeedState State(int nowHours) => new(true, 4, 2, 0, 48, 0, 0, false, -1, nowHours * Hour, nowHours / 24, nowHours % 24);

    private static IReadOnlyList<Warning> Check(NeedState state, int warnHours = 12, HashSet<string>? shown = null) => Warnings.Check(state, warnHours, shown ?? new HashSet<string>());

    [Fact]
    public void A_tier_drop_is_warned_once_it_is_within_the_warning_hours()
    {
        Assert.Empty(Check(State(35)));

        var warning = Assert.Single(Check(State(36)));

        Assert.Equal(WarningKind.TierDrop, warning.Kind);
        Assert.Equal(12, warning.HoursLeft);
    }

    [Fact]
    public void Each_warning_shows_once_and_a_send_that_moves_the_anchor_arms_the_next_one()
    {
        var shown = new HashSet<string>();

        Assert.Single(Check(State(40), shown: shown));
        Assert.Empty(Check(State(41), shown: shown));
        // A send at hour 41 moves the anchor there, so the next drop is at hour 89 and is warned from hour 77.
        Assert.Empty(Check(State(76) with { TierAnchorSeconds = 41 * Hour }, shown: shown));
        Assert.Single(Check(State(77) with { TierAnchorSeconds = 41 * Hour }, shown: shown));
    }

    [Fact]
    public void At_the_floor_nothing_drops_unless_the_floor_is_one_where_a_struggle_starts()
    {
        Assert.Empty(Check(State(40) with { Tier = 2 }));
        Assert.Equal(WarningKind.StruggleSoon, Assert.Single(Check(State(40) with { Tier = 1, TierFloor = 1 })).Kind);
    }

    [Fact]
    public void A_struggle_warns_with_the_hours_left_and_replaces_the_drop_warning()
    {
        var warning = Assert.Single(Check(State(40) with { StruggleState = 1, StruggleDeadlineSeconds = 45 * Hour }));

        Assert.Equal(WarningKind.Struggle, warning.Kind);
        Assert.Equal(5, warning.HoursLeft);
    }

    [Fact]
    public void Dying_warns()
    {
        Assert.Equal(WarningKind.Dying, Assert.Single(Check(State(10) with { StruggleState = 2 })).Kind);
    }

    [Fact]
    public void A_crisis_warns_before_the_start_of_its_deadline_day()
    {
        // Day 3, 14:00, deadline day 4: 10 h left.
        var state = State(3 * 24 + 14) with { TierAnchorSeconds = 3 * 24 * Hour + 14 * Hour, HasCrisis = true, CrisisDeadlineDay = 4 };

        Assert.Empty(Check(state, warnHours: 9));

        var warning = Assert.Single(Check(state, warnHours: 10));

        Assert.Equal(WarningKind.CrisisDeadline, warning.Kind);
        Assert.Equal(10, warning.HoursLeft);
    }

    [Fact]
    public void No_rescue_no_warnings()
    {
        Assert.Empty(Check(State(47) with { IsActive = false, StruggleState = 2 }));
    }

    [Fact]
    public void Hours_left_round_up_so_a_warning_never_says_zero_too_early()
    {
        var state = State(0) with { NowSeconds = 47 * Hour + 1 };

        Assert.Equal(1, Assert.Single(Check(state)).HoursLeft);
    }
}
