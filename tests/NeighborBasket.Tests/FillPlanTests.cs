using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class FillPlanTests
{
    private const int Tools = 4;
    private const int Cloth = 6;
    private const int Medicine = 9;
    private const int Food = FillPlan.FoodCategory;

    // Value alone, so a test about packing is not about the bonus.
    private static readonly Rates ValueOnly = new(0, 1f, 1f);

    private static BasketWish Wish(params int[] categories) => new(true, false, false, false, categories, null);

    private static Candidate Item(long id, int category, int value, int width = 1, int height = 1, int weight = 0, bool isForbidden = false) =>
        new(id, 100, (int)id, category, value, 1, isForbidden, weight, width, height);

    private static Basket Basket(int cols = 5, int rows = 3, float freeWeight = float.PositiveInfinity, Cells[]? taken = null, params int[] inBasket) =>
        new(cols, rows, taken ?? new Cells[0], freeWeight, inBasket);

    private static FillResult Plan(BasketWish wish, Candidate[] sources, Basket basket, Rates? rates = null) => FillPlan.Plan(wish, sources, basket, rates ?? Rates.Game);

    private static long[] Ids(FillResult result) => result.Picks.Select(pick => pick.Item.ItemId).OrderBy(id => id).ToArray();

    [Fact]
    public void It_packs_the_shapes_that_fill_the_grid_best_not_the_best_rate_per_cell_first()
    {
        // Best rate per cell first takes the 3x2 (12 a cell), then only the 2x3 fits: 132. The 3x3 and 2x3 fill all 15 cells: 159.
        var sources = new[] { Item(1, Tools, 72, 3, 2), Item(2, Tools, 99, 3, 3), Item(3, Tools, 60, 2, 3), Item(4, Tools, 36, 2, 2) };
        var result = Plan(Wish(Tools), sources, Basket(), ValueOnly);

        Assert.Equal(new long[] { 2, 3 }, Ids(result));
        Assert.Equal(159, result.Gain);
        AssertFits(result, Basket());
    }

    [Fact]
    public void Two_stacks_never_share_a_cell_even_when_that_would_score_more()
    {
        // The basket's right column is taken, so a 2x1 only goes in by crossing what is already there.
        var basket = Basket(2, 2, taken: new[] { new Cells(1, 0, 1, 2) });
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 100, 2, 1), Item(2, Tools, 1) }, basket, ValueOnly);

        Assert.Equal(new long[] { 2 }, Ids(result));
        Assert.Equal(0, result.Picks[0].X);
    }

    [Fact]
    public void A_wished_kind_beats_a_dearer_other_item_while_its_bonus_is_worth_more()
    {
        var oneCell = Basket(1, 1);

        // Tools: 15 + 10 x 0.2 = 17. Medicine at 200: 12, at 300: 18.
        Assert.Equal(new long[] { 1 }, Ids(Plan(Wish(Tools), new[] { Item(1, Tools, 10), Item(2, Medicine, 200) }, oneCell)));
        Assert.Equal(new long[] { 2 }, Ids(Plan(Wish(Tools), new[] { Item(1, Tools, 10), Item(2, Medicine, 300) }, oneCell)));
    }

    [Fact]
    public void The_bonus_counts_once_per_kind_and_the_gain_follows_the_game_formula()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 10), Item(2, Tools, 10), Item(3, Medicine, 50) }, Basket(3, 1));

        // 15 once, 2 + 2 for the tools, 3 for the medicine.
        Assert.Equal(22, result.Gain);
        Assert.Equal(3, result.Picks.Count);
    }

    [Fact]
    public void The_weight_left_is_never_passed()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 100, weight: 8), Item(2, Tools, 60, weight: 5), Item(3, Tools, 50, weight: 5) }, Basket(freeWeight: 10), ValueOnly);

        Assert.Equal(new long[] { 2, 3 }, Ids(result));
        Assert.True(result.Picks.Sum(pick => pick.Weight) <= 10);
    }

    [Fact]
    public void Cells_already_taken_stay_free_of_new_stacks()
    {
        var basket = Basket(2, 1, taken: new[] { new Cells(0, 0, 1, 1) });
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 100, 2, 1), Item(2, Tools, 5) }, basket);
        var pick = Assert.Single(result.Picks);

        Assert.Equal((2L, 1, 0), (pick.Item.ItemId, pick.X, pick.Y));
    }

    [Fact]
    public void Forbidden_and_valueless_items_are_never_offered()
    {
        var result = Plan(Wish(Tools), new[] { Item(1, Tools, 90, isForbidden: true), Item(2, Tools, 0), Item(3, Tools, 5) }, Basket());

        Assert.Equal(new long[] { 3 }, Ids(result));
    }

    [Fact]
    public void While_she_is_dying_only_food_goes_in_dearest_first()
    {
        var dying = new BasketWish(true, false, false, true, new[] { Tools }, null);
        var result = Plan(dying, new[] { Item(1, Food, 10), Item(2, Food, 20), Item(3, Food, 5), Item(4, Tools, 100) }, Basket(2, 1));

        Assert.True(result.IsFoodOnly);
        Assert.Equal(new long[] { 1, 2 }, Ids(result));
        Assert.Equal(30, result.Gain);
    }

    [Fact]
    public void Nothing_is_picked_with_no_neighbor_the_basket_away_her_wish_unknown_or_no_room()
    {
        var sources = new[] { Item(1, Tools, 10) };

        Assert.Equal(FillStop.NoNeighbor, Plan(BasketWish.None, sources, Basket()).Stop);
        Assert.Equal(FillStop.Away, Plan(new BasketWish(true, false, true, false, new[] { Tools }, null), sources, Basket()).Stop);
        Assert.Equal(FillStop.Pending, Plan(new BasketWish(true, true, false, false, new[] { Tools }, null), sources, Basket()).Stop);
        Assert.Equal(FillStop.Full, Plan(Wish(Tools), sources, Basket(taken: new[] { new Cells(0, 0, 5, 3) })).Stop);
        Assert.Equal(FillStop.Full, Plan(Wish(Tools), sources, Basket(freeWeight: 0)).Stop);
    }

    [Fact]
    public void An_unrevealed_wish_still_fills_her_trouble()
    {
        var wish = new BasketWish(true, true, false, false, new[] { Tools }, Medicine);
        var result = Plan(wish, new[] { Item(1, Tools, 10), Item(2, Medicine, 10) }, Basket(1, 1));

        Assert.Equal(new long[] { 2 }, Ids(result));
    }

    [Fact]
    public void A_wished_kind_with_nothing_at_hand_is_missing_unless_the_basket_has_it()
    {
        var result = Plan(Wish(Tools, Cloth, Medicine), new[] { Item(1, Tools, 10) }, Basket(inBasket: new[] { Medicine }));

        Assert.Equal(new[] { Cloth }, result.Missing);
    }

    [Fact]
    public void Score_skips_the_bonus_for_a_kind_already_in_the_basket()
    {
        var items = new[] { Item(1, Tools, 10), Item(2, Medicine, 100) };

        Assert.Equal(23, FillPlan.Score(items, Wish(Tools), new int[0], Rates.Game), 3);
        Assert.Equal(8, FillPlan.Score(items, Wish(Tools), new[] { Tools }, Rates.Game), 3);
    }

    private static void AssertFits(FillResult result, Basket basket)
    {
        var taken = new bool[basket.Cols, basket.Rows];

        foreach (var pick in result.Picks)
        {
            for (var x = pick.X; x < pick.X + pick.Item.Width; x++)
            {
                for (var y = pick.Y; y < pick.Y + pick.Item.Height; y++)
                {
                    Assert.InRange(x, 0, basket.Cols - 1);
                    Assert.InRange(y, 0, basket.Rows - 1);
                    Assert.False(taken[x, y], $"two stacks on {x},{y}");
                    taken[x, y] = true;
                }
            }
        }
    }
}
