using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class CardViewTests
{
    private const int Hour = 3600;
    private const int Tools = 4;
    private const int Cloth = 6;

    // Day 2, 10:00. Tier 4 over a floor of 2, anchored at hour 20, dropping every 48 h (at hour 68, so 10 h from now).
    private static NeedState State() => new(true, 4, 2, 20 * Hour, 48, 0, 0, false, -1, 58 * Hour, 2, 10);

    private static CardView.CardRow[] Rows(NeedState? state, Tracker? tracker = null, string? trouble = null, int warnHours = 12) =>
        CardView.Rows(state, tracker, trouble, warnHours, false).ToArray();

    private static Candidate Item(long id, int category, int value) => new(id, 100, (int)id, category, value, 1, false, 0, 1, 1);

    [Fact]
    public void The_condition_shows_the_tier_and_the_hours_until_it_drops_in_warn_colour_once_within_the_warning_hours()
    {
        var row = Rows(State())[0];

        Assert.Equal("Condition", row.Label);
        Assert.Equal("Tier 4", row.Tier);
        Assert.Equal("drops in 10 h", row.Value);
        Assert.Equal("warn", row.Tone);
        Assert.Equal("", Rows(State(), warnHours: 9)[0].Tone);
    }

    [Fact]
    public void At_a_floor_of_one_the_condition_says_when_she_starts_to_struggle_and_at_a_higher_floor_nothing_drops()
    {
        Assert.Equal("struggles in 10 h", Rows(State() with { Tier = 1, TierFloor = 1 })[0].Value);
        Assert.Equal("", Rows(State() with { Tier = 2 })[0].Value);
    }

    [Fact]
    public void While_she_struggles_the_card_shows_the_hours_left_and_the_satiety_she_needs()
    {
        var rows = Rows(State() with { StruggleState = 1, StruggleDeadlineSeconds = 63 * Hour, SatietyMin = 10, SatietyHigh = 100 });

        Assert.Equal(("Struggling, 5 h left", "bad"), (rows[0].Value, rows[0].Tone));
        Assert.Equal(("Satiety needed", "at least 10, better from 100"), (rows[1].Label, rows[1].Value));
        Assert.Equal(("Dying", "bad"), (Rows(State() with { StruggleState = 2 })[0].Value, Rows(State() with { StruggleState = 2 })[0].Tone));
    }

    [Fact]
    public void Her_trouble_shows_its_kind_and_the_hours_to_its_deadline_day()
    {
        // The deadline is the start of day 3, 14 h after day 2, 10:00.
        var row = Rows(State() with { HasCrisis = true, CrisisDeadlineDay = 3 }, trouble: "Medicine")[1];

        Assert.Equal(("Trouble: Medicine", "due in 14 h", "warn"), (row.Label, row.Value, row.Tone));
        Assert.Equal("bad", Rows(State() with { HasCrisis = true, CrisisDeadlineDay = 3 }, warnHours: 14)[1].Tone);
        Assert.Equal("Trouble", Rows(State() with { HasCrisis = true, CrisisDeadlineDay = 3 })[1].Label);
    }

    [Fact]
    public void The_streak_says_whether_today_is_sent_and_a_skipped_day_shows_it_broken()
    {
        var streak = State() with { StreakDays = 3, ConsecutiveSendDays = 2 };

        Assert.Equal(("2 / 3 days, send today", "warn"), Pick(Rows(streak with { LastSendDay = 1 }), "Send streak"));
        Assert.Equal(("2 / 3 days, sent today", ""), Pick(Rows(streak with { LastSendDay = 2 }), "Send streak"));
        Assert.Equal(("0 / 3 days, send today", ""), Pick(Rows(streak with { LastSendDay = 0 }), "Send streak"));
        Assert.DoesNotContain(Rows(State()), row => row.Label == "Send streak");
    }

    [Fact]
    public void Progress_and_affinity_come_from_the_tracker_and_no_rescue_shows_one_line()
    {
        var rows = Rows(State(), new Tracker(31, 50, 42, 100, 0, 0, 3));

        Assert.Equal(("42 / 100, affinity 31 (good ending at 50)", ""), Pick(rows, "Progress"));
        Assert.Equal("Nobody to help right now.", Assert.Single(Rows(null)).Label);
        Assert.Single(Rows(State() with { IsActive = false }));
    }

    [Fact]
    public void The_plan_line_says_how_many_stacks_fill_adds_and_the_gain()
    {
        var wish = new BasketWish(true, false, false, false, new[] { Tools, Cloth }, null);
        var sources = new[] { Item(1, Tools, 5), Item(2, Cloth, 5) };
        var plan = FillPlan.Plan(wish, sources, new Basket(5, 3, new Cells[0], float.PositiveInfinity, new int[0]), Rates.Game);
        var full = FillPlan.Plan(wish, sources, new Basket(5, 3, new[] { new Cells(0, 0, 5, 3) }, float.PositiveInfinity, new int[0]), Rates.Game);

        Assert.Equal("Fill adds 2 stacks from storage, about +32 progress.", CardView.PlanText(plan, false));
        Assert.Equal("The basket is full.", CardView.PlanText(full, false));
        Assert.Equal("Fill finds nothing at hand to add.", CardView.PlanText(new FillResult(new Pick[0], new[] { Tools }, FillStop.None, false), false));
    }

    private static (string Value, string Tone) Pick(CardView.CardRow[] rows, string label)
    {
        var row = Assert.Single(rows, row => row.Label == label);

        return (row.Value, row.Tone);
    }
}
