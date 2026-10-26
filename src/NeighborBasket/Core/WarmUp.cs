using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>
/// Runs the station, Fill, phone and alert builds once on made-up data, so the first basket opened in a game does not
/// pay for compiling them on a game frame. Pure .NET: it touches no game type and logs nothing, so it can run on a
/// background thread at load.
/// </summary>
public static class WarmUp
{
    public static void Run()
    {
        var sources = new List<StationItem>();

        for (var i = 0; i < 24; i++)
        {
            sources.Add(new StationItem(i + 1, i < 12 ? 100 : 200, 1000 + i, "item " + i, "", 1 + i % 4, 10 + i, 1 + i % 3, 1 + i % 2, 1 + i % 2, 1, i == 5));
        }

        var gift = new StationItem(99, 300, 2000, "gift", "", 2, 5, 1, 1, 1, 1, false, 0, 0, true);
        var data = new StationData(5, 3, 30, 1, new[] { gift }, sources, new[] { new Place(100, ""), new Place(200, "box") }, 100);
        var wish = new BasketWish(true, false, false, false, new[] { 1, 2 }, 3, 40);
        var need = new NeedState(true, 3, 1, 0, 48, 0, 0, true, 5, 3600, 1, 1, 0, 1, 3, 10, 100);
        var tracker = new Tracker(10, 50, 20, 100, 1, 3, 2);
        var names = new Dictionary<int, string> { [1] = "food", [2] = "tools", [3] = "medicine" };
        var home = sources.Where(item => item.OwnerId != data.BagId).Select(item => item.ToCandidate()).ToList();
        var inBasket = new[] { gift.ToCandidate() };
        var basket = data.ToBasket(wish, new HashSet<int> { gift.Category });
        var limits = FillPlan.Limits(home, inBasket, 30);
        var scoreNow = FillPlan.Score(inBasket, wish, new int[0], Rates.Game);
        var entries = new[] { new LogEntry(LogKind.Sent, 1, 8, "item 1 x2", 5, 1), new LogEntry(LogKind.Returned, 2, 9, "gift x1"), new LogEntry(LogKind.Said, 2, 9, "thanks") };

        foreach (var isChinese in new[] { false, true })
        {
            var plan = FillPlan.Goal(wish, home, basket, Rates.Game, 80, scoreNow, true, limits);

            FillPlan.Plan(wish, home, basket, Rates.Game, limits);
            StationView.Json(data, wish, need, tracker, "medicine", plan, 12, isChinese);
            Texts.Outcome(plan, 0, names, isChinese);
            AppView.Json(need, tracker, null, 12, entries, isChinese);
            HudView.Json(Warnings.Active(need, 12), isChinese);
            Warnings.Check(need, 12, new HashSet<string>());
            Print.Line(need, tracker);
            Print.Wish(wish, names);
        }
    }
}
