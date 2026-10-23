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

    /// <summary>Fill was clicked.</summary>
    public sealed record Fill : PageCommand;

    /// <summary>Call back was clicked.</summary>
    public sealed record CallBack : PageCommand;

    /// <summary>Put away was clicked.</summary>
    public sealed record PutAway : PageCommand;

    /// <summary>The basket card is showing and wants fresh data.</summary>
    public sealed record Card : PageCommand;

    /// <summary>The phone app opened or wants fresh data.</summary>
    public sealed record App : PageCommand;

    /// <summary>The page script just installed itself.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        return message.Substring(Prefix.Length) switch
        {
            "fill" => new Fill(),
            "callback" => new CallBack(),
            "putaway" => new PutAway(),
            "app" => new App(),
            "card" => new Card(),
            "ready" => new Ready(),
            "noscript" => new NoScript(),
            _ => new Unknown(message),
        };
    }
}
