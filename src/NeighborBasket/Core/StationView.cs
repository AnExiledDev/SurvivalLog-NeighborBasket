using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NeighborBasket.Core;

/// <summary>
/// One stack as the basket station shows it. Value is what the whole stack counts for when she scores the basket,
/// Weight the whole stack's weight in the game's units, Width and Height its cells, X and Y its top-left cell when it
/// sits in the basket. Icon is the game's own web icon path.
/// </summary>
public sealed record StationItem(long Id, long OwnerId, int ConfigId, string Name, string Icon, int Category, int Value, int Count, int Weight, int Width, int Height, bool IsForbidden, int X = 0, int Y = 0)
{
    public Candidate ToCandidate() => new(Id, OwnerId, ConfigId, Category, Value, Count, IsForbidden, Weight, Width, Height);

    public Cells Cells => new(X, Y, Width, Height);
}

/// <summary>A place items come from: the player's bag or one storage at home.</summary>
public sealed record Place(long Id, string Name);

/// <summary>
/// What the station shows: the basket's grid, weight and contents, and every stack at hand with the places they sit in.
/// BagId is the player's own bag, which Fill leaves alone. Weights are in the game's units.
/// </summary>
public sealed record StationData(int Cols, int Rows, float MaxWeight, float UsedWeight, IReadOnlyList<StationItem> InBasket, IReadOnlyList<StationItem> Sources, IReadOnlyList<Place> Places, long BagId)
{
    public Basket ToBasket(BasketWish wish, IReadOnlyCollection<int> categoriesInBasket) =>
        new(Cols, Rows, InBasket.Select(item => item.Cells).ToList(), MaxWeight > 0 ? System.Math.Max(0f, MaxWeight - UsedWeight) : float.PositiveInfinity, categoriesInBasket);
}

/// <summary>
/// Builds the basket station's data for the page: her status rows, what Fill would do and where its picks go, the
/// basket's contents, every place and every stack at hand, and the labels in the game's language. A stack is marked
/// wanted when she wishes for its category, and the stacks go out wanted first, then by value.
/// </summary>
public static class StationView
{
    public static string Json(StationData data, BasketWish wish, NeedState? need, Tracker? tracker, string? troubleName, FillResult? plan, int warnHours, bool isChinese)
    {
        var wanted = Wanted(wish);
        var json = new StringBuilder("{");

        json.Append("\"day\":").Append(JsText.Literal(need is { IsActive: true } ? Texts.AppDay.Say(isChinese, need.Day) : ""));
        json.Append(",\"rows\":[").Append(string.Join(",", CardView.Rows(need, tracker, troubleName, warnHours, isChinese).Select(CardView.RowJson))).Append(']');
        json.Append(",\"plan\":").Append(JsText.Literal(plan == null ? "" : CardView.PlanText(plan, isChinese)));
        json.Append(",\"gain\":").Append(plan?.Gain ?? 0);
        json.Append(",\"picks\":[").Append(string.Join(",", (plan?.Picks ?? new Pick[0]).Select(pick => "{\"id\":" + pick.Item.ItemId + ",\"x\":" + pick.X + ",\"y\":" + pick.Y + "}"))).Append(']');
        json.Append(",\"away\":").Append(wish.IsAway ? "true" : "false");
        json.Append(",\"dying\":").Append(wish.IsDying ? "true" : "false");
        json.Append(",\"cols\":").Append(data.Cols);
        json.Append(",\"gridRows\":").Append(data.Rows);
        json.Append(",\"max\":").Append(Number(data.MaxWeight));
        json.Append(",\"used\":").Append(Number(data.UsedWeight));
        json.Append(",\"bag\":").Append(data.BagId);
        json.Append(",\"basket\":[").Append(string.Join(",", data.InBasket.Select(item => ItemJson(item, wanted, true)))).Append(']');
        json.Append(",\"places\":[").Append(string.Join(",", data.Places.Select(place => "{\"id\":" + place.Id + ",\"name\":" + JsText.Literal(place.Name) + "}"))).Append(']');
        json.Append(",\"items\":[").Append(string.Join(",", Ordered(data.Sources, wanted).Select(item => ItemJson(item, wanted, false)))).Append(']');
        json.Append(",\"labels\":").Append(Labels(isChinese));

        return json.Append('}').ToString();
    }

    /// <summary>Wanted first, then the most valuable, then by name so the list holds still between refreshes.</summary>
    public static IEnumerable<StationItem> Ordered(IEnumerable<StationItem> items, ISet<int> wanted) =>
        items.OrderByDescending(item => wanted.Contains(item.Category) && !item.IsForbidden).ThenByDescending(item => item.Value).ThenBy(item => item.Name, System.StringComparer.Ordinal).ThenBy(item => item.Id);

    /// <summary>The categories she wants now: her trouble's, and her wishes once she has named them; none while she is dying.</summary>
    public static ISet<int> Wanted(BasketWish wish)
    {
        var wanted = new HashSet<int>();

        if (!wish.IsActive || wish.IsDying) return wanted;

        if (wish.CrisisCategory is { } crisis) wanted.Add(crisis);

        if (!wish.IsPending) wanted.UnionWith(wish.Categories);

        return wanted;
    }

    private static string ItemJson(StationItem item, ISet<int> wanted, bool isInBasket)
    {
        var json = new StringBuilder("{");

        json.Append("\"id\":").Append(item.Id);
        json.Append(",\"owner\":").Append(item.OwnerId);
        json.Append(",\"n\":").Append(JsText.Literal(item.Name));
        json.Append(",\"ic\":").Append(JsText.Literal(item.Icon));
        json.Append(",\"v\":").Append(item.Value);
        json.Append(",\"q\":").Append(item.Count);
        json.Append(",\"kg\":").Append(item.Weight);
        json.Append(",\"w\":").Append(item.Width);
        json.Append(",\"h\":").Append(item.Height);
        json.Append(",\"wanted\":").Append(wanted.Contains(item.Category) && !item.IsForbidden ? "true" : "false");
        json.Append(",\"no\":").Append(item.IsForbidden ? "true" : "false");

        if (isInBasket) json.Append(",\"x\":").Append(item.X).Append(",\"y\":").Append(item.Y);

        return json.Append('}').ToString();
    }

    private static string Labels(bool isChinese)
    {
        var labels = new (string Key, Phrase Text)[]
        {
            ("title", Texts.AppTitle), ("station", Texts.StationButton), ("game", Texts.GameScreen), ("takeFrom", Texts.TakeFrom), ("takeFromSub", Texts.TakeFromSub),
            ("allStorage", Texts.AllStorage), ("storage", Texts.StorageGroup), ("onYou", Texts.OnYou), ("bag", Texts.YourBag), ("sheWants", Texts.SheWants),
            ("everything", Texts.Everything), ("search", Texts.Search), ("wanted", Texts.WantedTag), ("nothing", Texts.NothingHere), ("nothingWanted", Texts.NothingWanted),
            ("addHint", Texts.AddHint), ("basket", Texts.BasketTitle), ("cells", Texts.Cells), ("takeHint", Texts.TakeHint), ("weight", Texts.WeightLabel),
            ("kgFree", Texts.KgFree), ("progress", Texts.AppProgress), ("ifSent", Texts.IfSent), ("send", Texts.SendButton), ("nextDoor", Texts.NextDoor),
            ("cannotTake", Texts.CannotTake), ("fill", Texts.FillButton), ("fillTitle", Texts.FillTitle), ("putAway", Texts.PutAwayButton),
            ("putAwayTitle", Texts.PutAwayTitle), ("callBack", Texts.CallBackButton), ("callBackTitle", Texts.CallBackTitle), ("away", Texts.Away),
        };

        return "{" + string.Join(",", labels.Select(label => JsText.Literal(label.Key) + ":" + JsText.Literal(label.Text.Pick(isChinese)))) + "}";
    }

    private static string Number(float value) => float.IsInfinity(value) || float.IsNaN(value) ? "0" : value.ToString("0.###", CultureInfo.InvariantCulture);
}
