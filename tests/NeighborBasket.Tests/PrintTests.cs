using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// The station and the phone app are built again only when the print of what they show changes. A print that misses
/// a shown value leaves the page stale; one that moves with the clock rebuilds them every second.
/// </summary>
public class PrintTests
{
    private const int Hour = 3600;

    // Her tier drops at 48 h from an anchor of 0, and she struggles until 100 h.
    private static NeedState Need(int now) => new(true, 3, 1, 0, 48, 1, 100 * Hour, false, -1, now, 2, 5, 1, 2, 3, 10, 100);

    private static readonly Tracker Standing = new(10, 50, 20, 100, 2, 3, 2);

    [Fact]
    public void Her_line_holds_still_while_the_clock_moves_within_the_hours_it_shows()
    {
        // 38 h to the drop and 90 h of struggle left show all through this hour.
        Assert.Equal(Print.Line(Need(10 * Hour), Standing), Print.Line(Need(10 * Hour + 1800), Standing));
        Assert.Equal(Print.Line(Need(10 * Hour), Standing), Print.Line(Need(11 * Hour - 1), Standing));
    }

    [Fact]
    public void Her_line_moves_when_an_hour_the_rows_show_ticks_over()
    {
        Assert.NotEqual(Print.Line(Need(11 * Hour - 1), Standing), Print.Line(Need(11 * Hour), Standing));
    }

    [Fact]
    public void Her_line_moves_with_every_value_her_rows_and_warnings_show()
    {
        var need = Need(10 * Hour);
        var line = Print.Line(need, Standing);

        Assert.NotEqual(line, Print.Line(need with { Tier = 2 }, Standing));
        Assert.NotEqual(line, Print.Line(need with { StruggleState = 2 }, Standing));
        Assert.NotEqual(line, Print.Line(need with { Hour24 = 6 }, Standing));
        Assert.NotEqual(line, Print.Line(need with { Day = 3 }, Standing));
        Assert.NotEqual(line, Print.Line(need with { LastSendDay = 2 }, Standing));
        Assert.NotEqual(line, Print.Line(need with { HasCrisis = true }, Standing));
        Assert.NotEqual(line, Print.Line(need, Standing with { Affinity = 11 }));
        Assert.NotEqual(line, Print.Line(need, Standing with { Progress = 21 }));
        Assert.NotEqual(line, Print.Line(need, Standing with { StrugglesLeft = 1 }));
        Assert.NotEqual(line, Print.Line(null, Standing));
        Assert.NotEqual(line, Print.Line(need, null));
    }

    [Fact]
    public void Her_wish_moves_with_what_she_wants_and_with_the_names_the_screen_gives_them()
    {
        var names = new Dictionary<int, string> { [4] = "Tools" };
        var wish = new BasketWish(true, false, false, false, new[] { 4 }, null, 40);
        var print = Print.Wish(wish, names);

        Assert.Equal(print, Print.Wish(new BasketWish(true, false, false, false, new[] { 4 }, null, 40), new Dictionary<int, string> { [4] = "Tools" }));
        Assert.NotEqual(print, Print.Wish(wish with { IsAway = true }, names));
        Assert.NotEqual(print, Print.Wish(new BasketWish(true, false, false, false, new[] { 4, 6 }, null, 40), names));
        Assert.NotEqual(print, Print.Wish(new BasketWish(true, false, false, false, new[] { 4 }, 9, 40), names));
        Assert.NotEqual(print, Print.Wish(new BasketWish(true, false, false, false, new[] { 4 }, null, 20), names));
        // A language switch renames the categories; the station must show the new names.
        Assert.NotEqual(print, Print.Wish(wish, new Dictionary<int, string> { [4] = "工具" }));
    }

    [Fact]
    public void Texts_with_the_same_letters_in_a_different_split_print_apart()
    {
        Assert.NotEqual(Print.Mix(Print.Mix(Print.Start, "ab"), "c"), Print.Mix(Print.Mix(Print.Start, "a"), "bc"));
        Assert.NotEqual(Print.Mix(Print.Start, (string?)null), Print.Mix(Print.Start, ""));
    }

    [Fact]
    public void The_warm_up_runs_without_the_game()
    {
        WarmUp.Run();
    }
}
