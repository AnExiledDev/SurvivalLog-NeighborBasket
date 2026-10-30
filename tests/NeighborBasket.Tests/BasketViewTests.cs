using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>The basket shows in the phone app and on the HUD whether or not she needs help right now.</summary>
public class BasketViewTests
{
    private static readonly BasketState Holding = new(false, new[] { "Apple x2", "Rope x1" });

    [Fact]
    public void With_nobody_to_help_the_app_still_says_what_the_basket_holds()
    {
        var json = AppView.Json(null, null, null, 12, new List<LogEntry>(), false, Holding);

        Assert.Contains("Nobody to help right now.", json);
        Assert.Contains("{\"label\":\"Basket\",\"value\":\"Apple x2, Rope x1\",\"tone\":\"warn\",\"tier\":\"\"}", json);
    }

    [Fact]
    public void During_a_rescue_the_basket_row_comes_after_her_status()
    {
        var need = new NeedState(true, 4, 2, 20 * 3600, 48, 0, 0, false, -1, 58 * 3600, 2, 10);

        var json = AppView.Json(need, new Tracker(12, 50, 30, 100, 2, 3, 1), null, 12, new List<LogEntry>(), false, Holding);

        Assert.True(json.IndexOf("Struggles left", StringComparison.Ordinal) < json.IndexOf("\"label\":\"Basket\"", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_basket_and_one_that_is_away_say_so()
    {
        Assert.Contains("\"value\":\"Empty\"", AppView.Json(null, null, null, 12, new List<LogEntry>(), false, new BasketState(false, Array.Empty<string>())));
        Assert.Contains("\"value\":\"With her, not back yet\"", AppView.Json(null, null, null, 12, new List<LogEntry>(), false, new BasketState(true, new[] { "Apple x2" })));
        Assert.Contains("\"value\":\"空的\"", AppView.Json(null, null, null, 12, new List<LogEntry>(), true, new BasketState(false, Array.Empty<string>())));
    }

    [Fact]
    public void No_basket_read_means_no_basket_row()
    {
        Assert.DoesNotContain("\"label\":\"Basket\"", AppView.Json(null, null, null, 12, new List<LogEntry>(), false));
    }

    [Fact]
    public void The_hud_has_a_basket_line_without_any_warning()
    {
        Assert.Equal("{\"basket\":{\"text\":\"In the basket: Apple x2, Rope x1\",\"title\":\"In her basket\",\"items\":[\"Apple x2\",\"Rope x1\"]}}",
            HudView.Json(Array.Empty<Warning>(), false, Holding));
        Assert.Contains("\"text\":\"篮子里有：Apple x2、Rope x1\"", HudView.Json(Array.Empty<Warning>(), true, Holding));
    }

    [Fact]
    public void The_hud_keeps_the_alert_and_adds_the_basket_beside_it()
    {
        // Day 1, 16:00; her trouble fails at the start of day 2.
        var state = new NeedState(true, 4, 2, 0, 46, 0, 0, true, 2, 40 * 3600, 1, 16);
        var json = HudView.Json(Warnings.Active(state, 12), false, Holding);

        Assert.StartsWith("{\"text\":\"Trouble runs out in 8 h\"", json);
        Assert.EndsWith(",\"basket\":{\"text\":\"In the basket: Apple x2, Rope x1\",\"title\":\"In her basket\",\"items\":[\"Apple x2\",\"Rope x1\"]}}", json);
    }

    [Fact]
    public void An_empty_or_away_basket_leaves_the_hud_alone()
    {
        Assert.Equal("null", HudView.Json(Array.Empty<Warning>(), false, new BasketState(false, Array.Empty<string>())));
        Assert.Equal("null", HudView.Json(Array.Empty<Warning>(), false, new BasketState(true, new[] { "Apple x2" })));
        Assert.Equal("null", HudView.Json(Array.Empty<Warning>(), false));
    }

    [Fact]
    public void The_print_changes_with_what_the_basket_shows()
    {
        var start = Print.Start;

        Assert.NotEqual(BasketView.Mix(start, Holding), BasketView.Mix(start, Holding with { IsAway = true }));
        Assert.NotEqual(BasketView.Mix(start, Holding), BasketView.Mix(start, new BasketState(false, new[] { "Apple x3", "Rope x1" })));
        Assert.NotEqual(BasketView.Mix(start, null), BasketView.Mix(start, new BasketState(false, Array.Empty<string>())));
        Assert.Equal(BasketView.Mix(start, Holding), BasketView.Mix(start, new BasketState(false, new[] { "Apple x2", "Rope x1" })));
    }
}
