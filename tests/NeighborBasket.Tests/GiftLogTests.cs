using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class GiftLogTests
{
    private static BasketLook Home(string items, int progress = 10, int affinity = 5, int day = 3, int hour = 9) => new(false, items, progress, affinity, day, hour);

    private static BasketLook Away(int progress = 10, int affinity = 5, int day = 3, int hour = 9) => new(true, "", progress, affinity, day, hour);

    [Fact]
    public void A_send_lists_what_left_and_what_it_gained_once_the_game_has_settled_it()
    {
        var watch = new GiftWatch();

        Assert.Empty(watch.See(Home("Rope x2", progress: 10, affinity: 5)));
        Assert.Empty(watch.See(Away(progress: 10, affinity: 5, hour: 10)));

        var sent = Assert.Single(watch.See(Away(progress: 37, affinity: 7, hour: 10)));

        Assert.Equal(new LogEntry(LogKind.Sent, 3, 10, "Rope x2", 27, 2), sent);
    }

    [Fact]
    public void A_return_lists_what_came_back_on_the_poll_after_it_got_home()
    {
        var watch = new GiftWatch();

        watch.See(Away());
        Assert.Empty(watch.See(Home("")));

        Assert.Equal(new LogEntry(LogKind.Returned, 4, 6, "Berries x3"), Assert.Single(watch.See(Home("Berries x3", day: 4, hour: 6))));
    }

    [Fact]
    public void The_first_poll_only_learns_the_state_so_loading_a_save_logs_nothing()
    {
        var watch = new GiftWatch();

        Assert.Empty(watch.See(Away()));
        Assert.Empty(watch.See(Away()));
        Assert.Empty(watch.See(Home("Rope x1")));
    }

    [Fact]
    public void A_line_round_trips_with_tabs_and_line_breaks_in_the_text()
    {
        var entry = new LogEntry(LogKind.Said, 12, 23, "a\tb\nc\\d", -3, 4);

        Assert.Equal(entry, LogEntry.Parse(entry.ToLine()));
        Assert.DoesNotContain("\n", entry.ToLine());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sent\t1\t2\t3\t4")]
    [InlineData("Lost\t1\t2\t3\t4\tx")]
    [InlineData("Sent\tone\t2\t3\t4\tx")]
    [InlineData("7\t1\t2\t3\t4\tx")]
    public void A_damaged_line_is_skipped(string? line)
    {
        Assert.Null(LogEntry.Parse(line));
    }

    [Fact]
    public void The_app_groups_entries_by_day_newest_first()
    {
        var entries = new[]
        {
            new LogEntry(LogKind.Sent, 1, 8, "Rope x1", 15, 1),
            new LogEntry(LogKind.Returned, 1, 20, "Berries x2"),
            new LogEntry(LogKind.Said, 2, 7, "Thank you"),
        };

        var json = AppView.Json(null, new Tracker(12, 50, 30, 100, 2, 3, 1), null, 12, entries, false);

        Assert.True(json.IndexOf("Day 2", StringComparison.Ordinal) < json.IndexOf("Day 1", StringComparison.Ordinal));
        Assert.True(json.IndexOf("Berries", StringComparison.Ordinal) < json.IndexOf("Rope", StringComparison.Ordinal));
        Assert.Contains("\"note\":\"+15 progress, +1 affinity\"", json);
        Assert.Contains("\"value\":\"12 (good ending at 50)\"", json);
        Assert.Contains("\"value\":\"2 / 3\"", json);
    }

    [Fact]
    public void The_app_shows_at_most_the_newest_entries()
    {
        var entries = Enumerable.Range(0, AppView.MaxEntries + 5).Select(i => new LogEntry(LogKind.Said, i, 0, "line" + i + ";")).ToList();

        var json = AppView.Json(null, null, null, 12, entries, false);

        Assert.Contains("line" + (AppView.MaxEntries + 4) + ";", json);
        Assert.DoesNotContain("\"line4;\"", json);
        Assert.Contains("\"line5;\"", json);
        Assert.Contains("Nobody to help right now.", json);
    }

    [Fact]
    public void With_a_live_rescue_the_app_shows_her_status_rows_with_their_tone_then_the_struggles_left()
    {
        // Day 2, 10:00. Tier 4 over a floor of 2, dropping at hour 68 (10 h away); a trouble due at the end of day 2.
        var need = new NeedState(true, 4, 2, 20 * 3600, 48, 0, 0, false, -1, 58 * 3600, 2, 10) with { HasCrisis = true, CrisisDeadlineDay = 2 };

        var json = AppView.Json(need, new Tracker(12, 50, 30, 100, 2, 3, 1), null, 12, new List<LogEntry>(), false);

        Assert.Contains("{\"label\":\"Condition\",\"value\":\"drops in 10 h\",\"tone\":\"warn\",\"tier\":\"Tier 4\"}", json);
        Assert.Contains("\"label\":\"Trouble\"", json);
        Assert.Contains("\"tone\":\"bad\"", json);
        Assert.True(json.IndexOf("Condition", StringComparison.Ordinal) < json.IndexOf("Struggles left", StringComparison.Ordinal));
        Assert.DoesNotContain("\"label\":\"Affinity\"", json);
    }
}
