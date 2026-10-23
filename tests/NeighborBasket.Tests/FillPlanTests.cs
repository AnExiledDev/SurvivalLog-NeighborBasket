using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class FillPlanTests
{
    private const int Tools = 4;
    private const int Cloth = 6;
    private const int Medicine = 9;
    private const int Food = FillPlan.FoodCategory;

    private static BasketWish Wish(params int[] categories) => new(true, false, false, false, categories, null);

    private static Candidate Item(long id, int category, int unitValue, int count = 1, bool isForbidden = false) => new(id, 100, (int)id, category, unitValue, count, isForbidden);

    private static FillResult Plan(BasketWish wish, Candidate[] sources, int freeCells = 5, bool movesOneUnit = true, params int[] inBasket) =>
        FillPlan.Plan(wish, sources, inBasket, freeCells, movesOneUnit);

    [Fact]
    public void Each_wished_category_gets_its_cheapest_item_since_a_hit_is_worth_more_than_value()
    {
        var result = Plan(Wish(Tools, Cloth), new[] { Item(1, Tools, 40), Item(2, Tools, 5), Item(3, Cloth, 30), Item(4, Cloth, 12), Item(5, Medicine, 1) });

        Assert.Equal(new long[] { 2, 4 }, result.Picks.Select(pick => pick.Item.ItemId));
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void Without_stacking_a_whole_stack_moves_so_the_stack_that_costs_least_in_total_wins()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 2, count: 20), Item(2, Tools, 10, count: 1) }, movesOneUnit: false);

        Assert.Equal(2, Assert.Single(result.Picks).Item.ItemId);
        Assert.False(result.Picks[0].IsOneUnit);
    }

    [Fact]
    public void With_stacking_one_unit_of_the_cheapest_stack_moves()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 2, count: 20), Item(2, Tools, 10, count: 1) }, movesOneUnit: true);

        Assert.Equal(1, Assert.Single(result.Picks).Item.ItemId);
        Assert.True(result.Picks[0].IsOneUnit);
    }

    [Fact]
    public void Forbidden_and_valueless_items_are_never_offered()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 1, isForbidden: true), Item(2, Tools, 0), Item(3, Tools, 50) });

        Assert.Equal(3, Assert.Single(result.Picks).Item.ItemId);
    }

    [Fact]
    public void The_crisis_category_goes_first_so_a_nearly_full_basket_still_resolves_it()
    {
        var wish = new BasketWish(true, false, false, false, new[] { Tools, Cloth }, Medicine);
        var result = Plan(wish, new[] { Item(1, Tools, 1), Item(2, Cloth, 1), Item(3, Medicine, 90) }, freeCells: 1);

        Assert.Equal(3, Assert.Single(result.Picks).Item.ItemId);
    }

    [Fact]
    public void Picks_never_exceed_the_free_cells()
    {
        var result = Plan(Wish(Tools, Cloth, Medicine), new[] { Item(1, Tools, 1), Item(2, Cloth, 1), Item(3, Medicine, 1) }, freeCells: 2);

        Assert.Equal(2, result.Picks.Count);
    }

    [Fact]
    public void A_category_already_in_the_basket_is_not_filled_again()
    {
        var result = Plan(Wish(Tools, Cloth), new[] { Item(1, Tools, 1), Item(2, Cloth, 1) }, 5, true, Tools);

        Assert.Equal(Cloth, Assert.Single(result.Picks).Item.Category);
    }

    [Fact]
    public void A_wished_category_with_nothing_at_hand_is_reported_missing()
    {
        var result = Plan(Wish(Tools, Cloth), new[] { Item(1, Tools, 1) });

        Assert.Equal(new[] { Cloth }, result.Missing);
    }

    [Fact]
    public void While_she_is_dying_only_food_goes_in_dearest_first_to_fill_her_most()
    {
        var wish = new BasketWish(true, false, false, true, new[] { Tools }, null);
        var result = Plan(wish, new[] { Item(1, Tools, 1), Item(2, Food, 5), Item(3, Food, 30), Item(4, Food, 12) }, freeCells: 2);

        Assert.True(result.IsFoodOnly);
        Assert.Equal(new long[] { 3, 4 }, result.Picks.Select(pick => pick.Item.ItemId));
    }

    [Fact]
    public void Nothing_is_picked_when_there_is_no_neighbor_the_basket_is_away_or_full()
    {
        var sources = new[] { Item(1, Tools, 1) };

        Assert.Equal(FillStop.NoNeighbor, Plan(BasketWish.None, sources).Stop);
        Assert.Equal(FillStop.Away, Plan(Wish(Tools) with { IsAway = true }, sources).Stop);
        Assert.Equal(FillStop.Full, Plan(Wish(Tools), sources, freeCells: 0).Stop);
    }

    [Fact]
    public void An_unrevealed_wish_fills_only_the_crisis()
    {
        var pending = new BasketWish(true, true, false, false, new[] { Tools }, null);

        Assert.Equal(FillStop.Pending, Plan(pending, new[] { Item(1, Tools, 1) }).Stop);
        Assert.Equal(Medicine, Assert.Single(Plan(pending with { CrisisCategory = Medicine }, new[] { Item(1, Tools, 1), Item(2, Medicine, 1) }).Picks).Item.Category);
    }

    [Fact]
    public void A_basket_that_already_has_every_wish_says_so()
    {
        Assert.Equal(FillStop.AllThere, Plan(Wish(Tools), new[] { Item(1, Tools, 1) }, 5, true, Tools).Stop);
    }
}
