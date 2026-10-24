using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

// Fill aims for a goal (80 by default, two steps of her condition) and stops there, instead of packing everything in.
public class GoalFillTests
{
    private const int Tools = 4;
    private const int Cloth = 6;
    private const int Medicine = 9;
    private const int Food = FillPlan.FoodCategory;

    private static BasketWish Wish(params int[] categories) => new(true, false, false, false, categories, null);

    private static Candidate Stack(long id, int category, int value, int count, int weight = 0, int width = 1, int height = 1) =>
        new(id, 100, (int)id, category, value, count, false, weight, width, height);

    private static Basket Basket(int cols = 5, int rows = 3, float freeWeight = float.PositiveInfinity, params int[] inBasket) =>
        new(cols, rows, new Cells[0], freeWeight, inBasket);

    private static FillResult Goal(BasketWish wish, Candidate[] sources, Basket? basket = null, int goal = 80, double scoreNow = 0, bool canSplit = true, IReadOnlyDictionary<int, int>? limits = null) =>
        FillPlan.Goal(wish, sources, basket ?? Basket(), Rates.Game, goal, scoreNow, canSplit, limits);

    private static double Score(FillResult result, BasketWish wish) => FillPlan.Score(result.Picks.Select(pick => pick.Item), wish, new int[0], Rates.Game);

    private static Pick PickOf(FillResult result, long id) => Assert.Single(result.Picks, pick => pick.Item.ItemId == id);

    [Fact]
    public void With_stacking_it_takes_only_the_items_the_goal_needs_not_the_whole_pile()
    {
        var wish = Wish(Tools, Cloth);
        var scrap = Stack(1, Tools, 300, 300);
        var cloth = Stack(2, Cloth, 50, 10);
        var result = Goal(wish, new[] { scrap, cloth, Stack(3, Medicine, 500, 5) });

        // One of each kind for the bonus: 15 + 0.2 and 15 + 1. The rest from the scrap pile at 0.2 an item: 246 more.
        Assert.Equal(1, PickOf(result, 2).Item.Count);
        Assert.Equal(247, PickOf(result, 1).Item.Count);
        Assert.True(PickOf(result, 1).IsPart);
        Assert.DoesNotContain(result.Picks, pick => pick.Item.ItemId == 3);
        Assert.InRange(Score(result, wish), 80, 81);
        Assert.Equal(80, result.Gain);
    }

    [Fact]
    public void One_of_each_wished_kind_goes_in_first_even_when_one_pile_could_reach_the_goal_alone()
    {
        var wish = Wish(Tools, Cloth, Medicine);
        var result = Goal(wish, new[] { Stack(1, Tools, 1000, 1000), Stack(2, Cloth, 40, 4), Stack(3, Medicine, 90, 3) });

        Assert.Equal(new[] { Tools, Cloth, Medicine }, result.Picks.Select(pick => pick.Item.Category).OrderBy(c => c).ToArray());
        Assert.Equal(1, PickOf(result, 2).Item.Count);
        Assert.Equal(1, PickOf(result, 3).Item.Count);
        // 45 from the bonuses, 2 + 6 from the cloth and medicine items' value, 0.2 a tool: the tools only fill the gap.
        Assert.InRange(Score(result, wish), 80, 81);
    }

    [Fact]
    public void Kinds_she_did_not_ask_for_go_in_only_once_the_wished_ones_run_out()
    {
        var wish = Wish(Tools);
        var result = Goal(wish, new[] { Stack(1, Tools, 10, 10), Stack(2, Medicine, 2000, 100) });

        // All ten tools: 15 + 2. Then medicine at 0.06 of 20 a piece, 1.2 each, for the 63 left: 53 pieces.
        Assert.Equal(10, PickOf(result, 1).Item.Count);
        Assert.False(PickOf(result, 1).IsPart);
        Assert.Equal(53, PickOf(result, 2).Item.Count);
        Assert.InRange(Score(result, wish), 80, 81);
    }

    [Fact]
    public void The_goal_never_passes_what_her_stage_still_needs()
    {
        var wish = new BasketWish(true, false, false, false, new[] { Tools }, null, StageLeft: 30);
        var result = Goal(wish, new[] { Stack(1, Tools, 500, 500) });

        // 15 + 0.2 a tool: 77 tools reach 30.4 (the plan aims a quarter over, for rounding).
        Assert.Equal(77, PickOf(result, 1).Item.Count);
        Assert.Equal(30, result.Gain);
    }

    [Fact]
    public void What_the_basket_already_holds_counts_toward_the_goal()
    {
        var sources = new[] { Stack(1, Tools, 500, 500) };

        Assert.Equal(FillStop.GoalMet, Goal(Wish(Tools), sources, scoreNow: 85).Stop);
        Assert.Empty(Goal(Wish(Tools), sources, scoreNow: 85).Picks);

        // 70 there already, tools in the basket so no bonus: 52 tools at 0.2 for the last 10 and the quarter over.
        var result = Goal(Wish(Tools), sources, Basket(inBasket: Tools), scoreNow: 70);

        Assert.Equal(52, PickOf(result, 1).Item.Count);
    }

    [Fact]
    public void Without_stacking_it_moves_whole_stacks_the_smallest_that_covers_the_gap()
    {
        var wish = Wish(Tools);
        var result = Goal(wish, new[] { Stack(1, Tools, 100, 10), Stack(2, Tools, 500, 10), Stack(3, Tools, 300, 10) }, canSplit: false);

        // The cheapest stack for the bonus (15 + 20), then the 300 (60) covers the 45 left; the 500 stays home.
        Assert.Equal(new long[] { 1, 3 }, result.Picks.Select(pick => pick.Item.ItemId).OrderBy(id => id).ToArray());
        Assert.All(result.Picks, pick => Assert.False(pick.IsPart));
        Assert.All(result.Picks, pick => Assert.Equal(10, pick.Item.Count));
    }

    [Fact]
    public void A_part_takes_no_more_items_than_the_weight_left_carries()
    {
        var result = Goal(Wish(Tools), new[] { Stack(1, Tools, 1000, 1000, weight: 1000) }, Basket(freeWeight: 20));

        Assert.Equal(20, PickOf(result, 1).Item.Count);
        Assert.True(result.Picks.Sum(pick => pick.Weight) <= 20);
    }

    [Fact]
    public void Each_part_and_whole_stack_gets_its_own_cell()
    {
        var result = Goal(Wish(Tools, Cloth), new[] { Stack(1, Tools, 300, 300), Stack(2, Cloth, 50, 10, width: 2, height: 2) }, Basket(3, 2));
        var cells = result.Picks.SelectMany(pick => Enumerable.Range(0, pick.Item.Width).SelectMany(dx => Enumerable.Range(0, pick.Item.Height).Select(dy => (pick.X + dx, pick.Y + dy)))).ToList();

        Assert.Equal(2, result.Picks.Count);
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, cell => Assert.True(cell.Item1 < 3 && cell.Item2 < 2));
    }

    [Fact]
    public void While_she_is_dying_it_still_packs_the_most_food()
    {
        var dying = new BasketWish(true, false, false, true, new[] { Tools }, null);
        var result = Goal(dying, new[] { Stack(1, Food, 30, 3), Stack(2, Tools, 900, 9) }, Basket(1, 1));

        Assert.True(result.IsFoodOnly);
        Assert.Equal(3, PickOf(result, 1).Item.Count);
    }

    [Fact]
    public void A_goal_of_zero_packs_the_basket_full_as_before()
    {
        var sources = new[] { Stack(1, Tools, 300, 300), Stack(2, Tools, 200, 200) };
        var full = FillPlan.Plan(Wish(Tools), sources, Basket(), Rates.Game);

        Assert.Equal(full.Gain, Goal(Wish(Tools), sources, goal: 0).Gain);
        Assert.Equal(115, full.Gain);
    }

    [Fact]
    public void Every_item_of_a_stack_adds_the_same_so_more_of_one_kind_never_counts_for_less()
    {
        var stack = Stack(1, Tools, 30, 30);
        var held = FillPlan.ItemGain(stack, Wish(Tools), new[] { Tools }, Rates.Game);
        var fresh = FillPlan.ItemGain(stack, Wish(Tools), new int[0], Rates.Game);

        Assert.Equal(0.2, held.One, 6);
        Assert.Equal(6.0, held.Stack, 6);
        Assert.Equal(held.One * 30, held.Stack, 6);
        // The first of a kind she wants brings the bonus once, never once per item.
        Assert.Equal(15.2, fresh.One, 6);
        Assert.Equal(21.0, fresh.Stack, 6);
        Assert.Equal(0.06, FillPlan.ItemGain(Stack(2, Medicine, 10, 10), Wish(Tools), new int[0], Rates.Game).One, 6);
    }

    private const int Seeds = 12;

    private static Candidate Seed(long id, int config, int count, int unitValue = 10) => new(id, 100, config, Seeds, unitValue * count, count, false, 0, 1, 1);

    [Fact]
    public void The_cap_is_a_share_of_what_you_have_rounded_down_less_what_the_basket_holds()
    {
        var home = new[] { Seed(1, 501, 10), Seed(2, 501, 6), Seed(3, 502, 3), Seed(4, 503, 14) };
        var limits = FillPlan.Limits(home, new[] { Seed(9, 503, 2) }, 30)!;

        Assert.Equal(4, limits[501]);
        Assert.Equal(0, limits[502]);
        // 14 at home and 2 in the basket: 30% of 16 is 4, 2 of them already in.
        Assert.Equal(2, limits[503]);
        Assert.Null(FillPlan.Limits(home, new Candidate[0], 100));
    }

    [Fact]
    public void Fill_never_takes_more_of_one_item_than_its_cap_even_when_that_item_is_worth_the_most()
    {
        var sources = new[] { Seed(1, 501, 16, 30), Seed(2, 502, 12, 20), Seed(3, 503, 11, 20) };
        var limits = FillPlan.Limits(sources, new Candidate[0], 30);
        var result = Goal(Wish(Seeds), sources, limits: limits);

        Assert.Equal(4, PickOf(result, 1).Item.Count);
        Assert.Equal(3, PickOf(result, 2).Item.Count);
        Assert.Equal(3, PickOf(result, 3).Item.Count);
        Assert.All(result.Picks, pick => Assert.True(pick.IsPart));
    }

    [Fact]
    public void Without_stacking_a_stack_bigger_than_its_cap_stays_home()
    {
        var sources = new[] { Seed(1, 501, 16, 30), Seed(2, 501, 3, 30), Seed(3, 502, 20, 1) };
        var limits = FillPlan.Limits(sources, new Candidate[0], 30);
        var result = Goal(Wish(Seeds), sources, limits: limits, canSplit: false);

        Assert.Contains(result.Picks, pick => pick.Item.ItemId == 2);
        Assert.DoesNotContain(result.Picks, pick => pick.Item.ItemId == 1);
        Assert.DoesNotContain(result.Picks, pick => pick.Item.ItemId == 3);
    }

    [Fact]
    public void Shift_fill_keeps_to_the_cap_too()
    {
        var sources = new[] { Seed(1, 501, 3), Seed(2, 501, 3), Seed(3, 501, 3) };
        var result = FillPlan.Plan(Wish(Seeds), sources, Basket(), Rates.Game, FillPlan.Limits(sources, new Candidate[0], 70));

        // 70% of 9 is 6: two of the three stacks.
        Assert.Equal(2, result.Picks.Count);
    }

    // The cap guards stock in a normal round; a dying neighbour needs the food, and one stack of it is the usual shape.
    [Fact]
    public void While_she_is_dying_the_cap_does_not_hold_back_her_food()
    {
        var dying = new BasketWish(true, false, false, true, new[] { Tools }, null);
        var sources = new[] { Stack(1, Food, 30, 15) };
        var limits = FillPlan.Limits(sources, new Candidate[0], 30);

        var goal = Goal(dying, sources, limits: limits);
        var shift = FillPlan.Plan(dying, sources, Basket(), Rates.Game, limits);

        Assert.Equal(15, PickOf(goal, 1).Item.Count);
        Assert.Empty(goal.Missing);
        Assert.Equal(15, PickOf(shift, 1).Item.Count);
    }

    // A kind the cap holds back is at home, so Fill must not tell the player there is none of it.
    [Fact]
    public void A_kind_held_back_by_the_cap_is_reported_as_capped_not_missing()
    {
        var sources = new[] { Seed(1, 501, 10), Stack(2, Tools, 300, 10) };
        var limits = FillPlan.Limits(sources, new Candidate[0], 30);

        var shift = FillPlan.Plan(Wish(Seeds, Tools), sources, Basket(), Rates.Game, limits);
        var whole = Goal(Wish(Seeds, Tools), sources, limits: new Dictionary<int, int>(), canSplit: false);

        Assert.Empty(shift.Picks);
        Assert.Empty(shift.Missing);
        Assert.Equal(FillStop.Capped, shift.Stop);
        Assert.Empty(whole.Missing);
        Assert.Equal(FillStop.Capped, whole.Stop);
    }
}
