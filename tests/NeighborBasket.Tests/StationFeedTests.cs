using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class StationFeedTests
{
    [Fact]
    public void An_answer_the_page_already_has_is_not_sent_again()
    {
        var feed = new StationFeed();

        Assert.Equal("{\"a\":1}", feed.Next("{\"a\":1}", false));
        Assert.Null(feed.Next("{\"a\":1}", false));
        Assert.Equal("{\"a\":2}", feed.Next("{\"a\":2}", false));
    }

    // The bug: closing and opening the basket within a few seconds left the game's own screen up, because the page had
    // dropped its data on close and the plugin would not send the same answer again.
    [Fact]
    public void A_page_that_just_opened_gets_the_answer_even_when_it_is_the_same_as_last_time()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false);

        Assert.Equal("{\"a\":1}", feed.Next("{\"a\":1}", true));
    }

    // The bug: an ask that landed while the basket screen was closing got "null", which switched the station off until
    // the game restarted.
    [Fact]
    public void A_closed_basket_screen_sends_nothing_and_the_next_open_answer_still_goes_out()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false);

        Assert.Null(feed.Next(null, true));
        Assert.Equal("{\"a\":2}", feed.Next("{\"a\":2}", false));
    }

    [Fact]
    public void A_move_the_page_already_showed_gets_its_answer_even_when_nothing_changed()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false);
        feed.Forget();

        Assert.Equal("{\"a\":1}", feed.Next("{\"a\":1}", false));
    }

    // The station asks once a second; an answer drawn from the same things as the last one is not built at all.
    [Fact]
    public void An_unchanged_print_skips_the_build_and_sends_nothing()
    {
        var feed = new StationFeed();

        Assert.False(feed.IsUnchanged(7, false, out _));
        feed.Next("{\"a\":1}", false, 7);

        Assert.True(feed.IsUnchanged(7, false, out var resend));
        Assert.Null(resend);
    }

    [Fact]
    public void A_changed_print_is_built_and_sent()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false, 7);

        Assert.False(feed.IsUnchanged(8, false, out _));
        Assert.Equal("{\"a\":2}", feed.Next("{\"a\":2}", false, 8));
        Assert.True(feed.IsUnchanged(8, false, out _));
    }

    [Fact]
    public void A_page_that_asks_afresh_with_an_unchanged_print_gets_the_last_answer_without_a_build()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false, 7);

        Assert.True(feed.IsUnchanged(7, true, out var resend));
        Assert.Equal("{\"a\":1}", resend);
    }

    // A move the page drew, or a language switch, must get a built answer even when the print held still.
    [Fact]
    public void After_forget_the_same_print_is_built_and_sent_again()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false, 7);
        feed.Forget();

        Assert.False(feed.IsUnchanged(7, false, out _));
        Assert.Equal("{\"a\":1}", feed.Next("{\"a\":1}", false, 7));
    }

    // While the basket screen is closed nothing is built, so the print from before it closed still stands.
    [Fact]
    public void An_answer_without_a_print_is_built_again_next_time()
    {
        var feed = new StationFeed();

        feed.Next("{\"a\":1}", false);

        Assert.False(feed.IsUnchanged(0, false, out _));
    }

    [Fact]
    public void A_fill_finishes_once_every_stack_it_placed_is_in_the_basket_or_the_wait_runs_out()
    {
        var placed = new long[] { 1, 2 };

        Assert.False(FillWait.IsDone(placed, new HashSet<long> { 1 }, 0.1f, 0.6f));
        Assert.True(FillWait.IsDone(placed, new HashSet<long> { 1, 2, 9 }, 0.1f, 0.6f));
        Assert.True(FillWait.IsDone(placed, new HashSet<long> { 1 }, 0.6f, 0.6f));
        Assert.True(FillWait.IsDone(new long[0], new HashSet<long>(), 0f, 0.6f));
    }
}
