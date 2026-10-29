using System;

namespace NeighborBasket.Core;

/// <summary>
/// What the basket panel's buttons ask for. The page sends "slmod|neighborbasket|" followed by a verb; anything else
/// parses to Unknown so it can be logged rather than acted on (the page's "diag|..." notes land there on purpose).
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|neighborbasket|";

    private PageCommand()
    {
    }

    /// <summary>Fill was clicked; IsMax when Shift was held, for the most the basket holds instead of the goal.</summary>
    public sealed record Fill(bool IsMax = false) : PageCommand;

    /// <summary>Call back was clicked.</summary>
    public sealed record CallBack : PageCommand;

    /// <summary>Put away was clicked.</summary>
    public sealed record PutAway : PageCommand;

    /// <summary>The basket station is showing and wants fresh data; IsFresh when it holds none, so even an unchanged answer goes out.</summary>
    public sealed record Station(bool IsFresh = false) : PageCommand;

    /// <summary>A stack was clicked in the station: one item of it, or the whole stack, goes in the basket.</summary>
    public sealed record Add(long ItemId, bool IsWholeStack) : PageCommand;

    /// <summary>A stack in the basket was clicked: it goes back where it came from.</summary>
    public sealed record Take(long ItemId) : PageCommand;

    /// <summary>To bag was clicked: everything in the basket goes to the bag.</summary>
    public sealed record TakeAll : PageCommand;

    /// <summary>Break down was clicked.</summary>
    public sealed record BreakDown : PageCommand;

    /// <summary>The phone app wants its data: only when it changed, or always with IsFresh, when the page holds none.</summary>
    public sealed record App(bool IsFresh = false) : PageCommand;

    /// <summary>The page script just installed itself.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>The station's search box took the keyboard (IsOn) or let it go.</summary>
    public sealed record Focus(bool IsOn) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        if (parts.Length == 3 && parts[0] == "add" && Id(parts[1]) is { } added && parts[2] is "one" or "all") return new Add(added, parts[2] == "all");
        if (parts.Length == 2 && parts[0] == "take" && Id(parts[1]) is { } taken) return new Take(taken);
        if (parts.Length == 2 && parts[0] == "focus" && parts[1] is "0" or "1") return new Focus(parts[1] == "1");

        return message.Substring(Prefix.Length) switch
        {
            "fill" => new Fill(),
            "fillmax" => new Fill(true),
            "callback" => new CallBack(),
            "putaway" => new PutAway(),
            "takeall" => new TakeAll(),
            "breakdown" => new BreakDown(),
            "app" => new App(),
            "app|new" => new App(true),
            "station" => new Station(),
            "station|new" => new Station(true),
            "ready" => new Ready(),
            "noscript" => new NoScript(),
            _ => new Unknown(message),
        };
    }

    private static long? Id(string text) => long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
