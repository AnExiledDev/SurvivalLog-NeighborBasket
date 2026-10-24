using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class BreakdownTests
{
    private const int Material = Breakdown.MaterialCategory;
    private const int Fertilizer = 11;

    [Fact]
    public void A_gift_whose_use_only_gives_materials_breaks_down()
    {
        // The wind chime: "take apart" gives 4 tin sheets and nothing else.
        Assert.True(Breakdown.IsBreakable(new UseInfo(false, 100024124, false, false, new[] { Material })));
        // The carved bird gives tin, plastic and glass.
        Assert.True(Breakdown.IsBreakable(new UseInfo(false, 100042107, false, false, new[] { Material, Material, Material })));
    }

    [Fact]
    public void Uses_that_give_something_else_or_do_something_else_do_not()
    {
        // The small flower turns into fertilizer: a use, not a break-down.
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 100024115, false, false, new[] { Fertilizer })));
        // Materials plus anything else is not a pure break-down either.
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 1, false, false, new[] { Material, Fertilizer })));
        // Canned food gives back a tin sheet but also feeds: eating it is not breaking it down.
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 100002115, true, false, new[] { Material })));
        // A random roll, a use the game forbids, no use and a use with no output all fail.
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 1, false, true, new[] { Material })));
        Assert.False(Breakdown.IsBreakable(new UseInfo(true, 1, false, false, new[] { Material })));
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 0, false, false, new[] { Material })));
        // A stackable item would merge into a bag stack, and one Use takes apart only one of it.
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 1, false, false, new[] { Material }, 5)));
        Assert.False(Breakdown.IsBreakable(new UseInfo(false, 1, false, false, new int[0])));
        Assert.False(Breakdown.IsBreakable(null));
    }

    [Fact]
    public void Only_breakable_stacks_in_the_basket_are_marked_for_the_page()
    {
        var chime = new StationItem(1, 9, 24124, "chime", "i.png", 19, 50, 1, 150, 2, 2, false, 0, 0, true);
        var food = new StationItem(2, 9, 2115, "can", "i.png", 1, 10, 1, 300, 1, 1, false, 2, 0);
        var json = StationView.Json(new StationData(5, 3, 0, 0, new[] { chime, food }, new StationItem[0], new Place[0], 7), new BasketWish(true, false, false, false, new int[0], null), null, null, null, null, 12, false);

        Assert.Contains("\"id\":1,", json);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "\"br\":true").Count);
        Assert.Matches("\"id\":1,[^}]*\"br\":true", json);
    }
}
