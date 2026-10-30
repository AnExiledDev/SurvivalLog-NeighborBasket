using System.Collections.Generic;
using System.Text;

namespace NeighborBasket.Core;

/// <summary>The basket at home as last read: away with her or not, and what it holds as "name x count", in a stable order.</summary>
public sealed record BasketState(bool IsAway, IReadOnlyList<string> Items)
{
    public bool HasItems => !IsAway && Items.Count > 0;
}

/// <summary>
/// What the phone app and the HUD say about the basket, with or without a rescue running: the app always has a row for
/// it (away, empty, or what it holds); the HUD has a line only while it is home holding something.
/// </summary>
public static class BasketView
{
    public static CardView.CardRow Row(BasketState basket, bool isChinese)
    {
        var value = basket.IsAway ? Texts.BasketAwayValue.Pick(isChinese)
            : basket.Items.Count == 0 ? Texts.BasketEmptyValue.Pick(isChinese)
            : List(basket.Items, isChinese);

        return new CardView.CardRow(Texts.BasketTitle.Pick(isChinese), value, basket.HasItems ? "warn" : "");
    }

    /// <summary>The HUD's basket line, or null when there is nothing to show.</summary>
    public static string? HudJson(BasketState? basket, bool isChinese)
    {
        if (basket is not { HasItems: true } held) return null;

        var json = new StringBuilder("{\"text\":").Append(JsText.Literal(Texts.HudBasket.Say(isChinese, List(held.Items, isChinese))));

        json.Append(",\"title\":").Append(JsText.Literal(Texts.HudBasketTitle.Pick(isChinese)));
        json.Append(",\"items\":[");

        for (var i = 0; i < held.Items.Count; i++)
        {
            if (i > 0) json.Append(',');

            json.Append(JsText.Literal(held.Items[i]));
        }

        return json.Append("]}").ToString();
    }

    /// <summary>Everything the basket's row and line show, for a print.</summary>
    public static ulong Mix(ulong print, BasketState? basket)
    {
        if (basket == null) return Print.Mix(print, -1L);

        print = Print.Mix(print, basket.IsAway);

        foreach (var item in basket.Items) print = Print.Mix(print, item);

        return Print.Mix(print, basket.Items.Count);
    }

    private static string List(IReadOnlyList<string> items, bool isChinese) => string.Join(isChinese ? "、" : ", ", items);
}
