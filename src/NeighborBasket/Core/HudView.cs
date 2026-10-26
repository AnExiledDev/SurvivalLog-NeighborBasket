using System.Collections.Generic;
using System.Text;

namespace NeighborBasket.Core;

/// <summary>
/// The alert line on the game's Rescue panel: the most urgent warning in short, red when her life is at stake and
/// amber otherwise, "+N more" when there are others, and every warning in full for the hover list. "null" when nothing
/// is due, which takes the line away.
/// </summary>
public static class HudView
{
    public static string Json(IReadOnlyList<Warning> warnings, bool isChinese)
    {
        if (warnings.Count == 0) return "null";

        var first = warnings[0];
        var json = new StringBuilder("{\"text\":").Append(JsText.Literal(Texts.Hud(first, isChinese)));

        json.Append(",\"tone\":").Append(Warnings.IsSevere(first.Kind) ? "\"bad\"" : "\"warn\"");
        json.Append(",\"more\":").Append(JsText.Literal(warnings.Count > 1 ? Texts.HudMore.Say(isChinese, warnings.Count - 1) : ""));
        json.Append(",\"title\":").Append(JsText.Literal(Texts.HudTitle.Pick(isChinese)));
        json.Append(",\"all\":[");

        for (var i = 0; i < warnings.Count; i++)
        {
            if (i > 0) json.Append(',');

            json.Append("{\"text\":").Append(JsText.Literal(Texts.Warn(warnings[i], isChinese)))
                .Append(",\"tone\":").Append(Warnings.IsSevere(warnings[i].Kind) ? "\"bad\"" : "\"warn\"").Append('}');
        }

        return json.Append("]}").ToString();
    }
}
