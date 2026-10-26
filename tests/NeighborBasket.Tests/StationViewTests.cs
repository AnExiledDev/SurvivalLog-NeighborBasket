using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class StationViewTests
{
    private const int Tools = 4;
    private const int Cloth = 6;
    private const int Medicine = 9;

    private static StationItem Item(long id, int category, int value, string name = "x", bool isForbidden = false, int x = 0, int y = 0) =>
        new(id, 7, (int)id, name, "../../Res/i.png", category, value, 1, 2, 1, 1, isForbidden, x, y);

    [Fact]
    public void She_wants_her_trouble_and_her_named_wishes_and_nothing_while_dying()
    {
        Assert.Equal(new[] { Tools, Medicine }, StationView.Wanted(new BasketWish(true, false, false, false, new[] { Tools }, Medicine)).OrderBy(c => c));
        Assert.Equal(new[] { Medicine }, StationView.Wanted(new BasketWish(true, true, false, false, new[] { Tools }, Medicine)));
        Assert.Empty(StationView.Wanted(new BasketWish(true, false, false, true, new[] { Tools }, Medicine)));
        Assert.Empty(StationView.Wanted(BasketWish.None));
    }

    [Fact]
    public void Wanted_stacks_list_first_then_the_dearest_and_a_forbidden_one_is_never_wanted()
    {
        var items = new[] { Item(1, Cloth, 90), Item(2, Tools, 5), Item(3, Tools, 50), Item(4, Tools, 99, isForbidden: true) };
        var ordered = StationView.Ordered(items, new HashSet<int> { Tools }).Select(item => item.Id);

        Assert.Equal(new long[] { 3, 2, 4, 1 }, ordered);
    }

    [Fact]
    public void Stacks_list_by_the_progress_one_item_adds_then_the_whole_stack()
    {
        var wish = new BasketWish(true, false, false, false, new[] { Tools }, null);
        var cheapTool = new StationItem(1, 7, 501, "a", "i.png", Tools, 10, 10, 2, 1, 1, false);
        var dearTool = new StationItem(2, 7, 502, "b", "i.png", Tools, 100, 10, 2, 1, 1, false);
        var bigPile = new StationItem(3, 7, 503, "c", "i.png", Tools, 200, 100, 2, 1, 1, false);
        var cloth = new StationItem(4, 7, 504, "d", "i.png", Cloth, 900, 1, 2, 1, 1, false);
        var data = new StationData(5, 3, 0, 0, new StationItem[0], new[] { cheapTool, bigPile, cloth, dearTool }, new[] { new Place(7, "") }, 7);
        var json = StationView.Json(data, wish, null, null, null, null, 12, false);
        var order = new[] { 1, 2, 3, 4 }.OrderBy(id => json.IndexOf("\"id\":" + id + ",\"owner\"", System.StringComparison.Ordinal)).ToArray();

        // One cloth is 54 at the other rate, no bonus but still the most. One tool brings the bonus: 15 + 2 for the dear
        // one, 15 + 0.4 for one of the big pile, 15 + 0.2 for the cheap one.
        Assert.Equal(new[] { 4, 2, 3, 1 }, order);
    }

    [Fact]
    public void The_json_carries_the_grid_the_basket_cells_the_planned_picks_and_wanted_marks()
    {
        var inBasket = Item(10, Tools, 5, x: 2, y: 1);
        var source = Item(11, Tools, 30);
        var data = new StationData(5, 3, 0, 0, new[] { inBasket }, new[] { source, Item(12, Cloth, 3, x: 4, y: 2) }, new[] { new Place(7, "") }, 7);
        var plan = new FillResult(new[] { new Pick(source.ToCandidate(), 3, 0) }, new int[0], FillStop.None, false, 21);
        var json = StationView.Json(data, new BasketWish(true, false, false, false, new[] { Tools }, null), null, null, null, plan, 12, false);

        Assert.Contains("\"cols\":5,\"gridRows\":3,\"max\":0,\"used\":0,\"bag\":7", json);
        Assert.Contains("\"picks\":[{\"id\":11,\"x\":3,\"y\":0,\"q\":1}]", json);
        Assert.Contains("\"id\":10,", json);
        Assert.Contains("\"wanted\":true,\"no\":false,\"x\":2,\"y\":1}", json);
        Assert.Contains("\"id\":12,\"owner\":7,\"cfg\":12,\"n\":\"x\",\"ic\":\"../../Res/i.png\",\"q\":1,\"kg\":2,\"w\":1,\"h\":1,\"wanted\":false", json);
        // A source stack carries its cell too: the page keeps a row by where the stack sits, because taking one unit
        // with Stacking leaves the rest on that cell under a new id.
        Assert.Matches("\"id\":12,[^}]*\"x\":4,\"y\":2}", json);
        Assert.Contains("\"gain\":21", json);
        Assert.Contains("\"send\":\"Send basket\"", json);
    }

    [Fact]
    public void Each_stack_at_hand_says_what_one_item_and_the_whole_stack_would_add()
    {
        var tools = new StationItem(11, 7, 501, "Scrap", "i.png", Tools, 30, 30, 2, 1, 1, false);
        var cloth = new StationItem(12, 7, 502, "Rag", "i.png", Cloth, 30, 30, 2, 1, 1, false);
        var refused = new StationItem(13, 7, 503, "Bomb", "i.png", Tools, 30, 30, 2, 1, 1, true);
        var data = new StationData(5, 3, 0, 0, new[] { Item(10, Cloth, 5) }, new[] { tools, cloth, refused }, new[] { new Place(7, "") }, 7);
        var json = StationView.Json(data, new BasketWish(true, false, false, false, new[] { Tools, Cloth }, null), null, null, null, null, 12, false);

        // Tools are not in the basket yet: the first brings the bonus (15.2, whole above 10). Cloth is there already: 0.2 an item.
        Assert.Contains("\"id\":11,\"owner\":7,\"cfg\":501,", json);
        Assert.Contains("\"g1\":15,\"gs\":21,\"x\":0,\"y\":0}", json);
        Assert.Contains("\"g1\":0.2,\"gs\":6,\"x\":0,\"y\":0}", json);
        Assert.Contains("\"no\":true,\"x\":0,\"y\":0}", json);
        Assert.Equal("1.5", StationView.Gain(1.46));
        Assert.Equal("15", StationView.Gain(15.4));
    }

    [Fact]
    public void A_basket_with_no_weight_limit_never_runs_out_of_weight()
    {
        var empty = new StationItem[0];

        Assert.True(float.IsPositiveInfinity(new StationData(5, 3, 0, 0, empty, empty, new Place[0], 0).ToBasket(BasketWish.None, new int[0]).FreeWeight));
        Assert.Equal(6f, new StationData(5, 3, 10, 4, empty, empty, new Place[0], 0).ToBasket(BasketWish.None, new int[0]).FreeWeight);
        Assert.Equal(new Cells(2, 1, 1, 1), Assert.Single(new StationData(5, 3, 0, 0, new[] { Item(1, Tools, 1, x: 2, y: 1) }, empty, new Place[0], 0).ToBasket(BasketWish.None, new int[0]).Taken));
    }
}
