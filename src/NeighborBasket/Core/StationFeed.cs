using System.Collections.Generic;

namespace NeighborBasket.Core;

/// <summary>
/// Which basket station answers go to the page. The page keeps the last answer, so one equal to it is skipped, unless
/// the page asks afresh because it holds nothing (it just opened or reloaded) or already drew a move it is waiting to
/// hear back on. While the basket screen is not open nothing is sent: the page can still think it is open for a moment
/// as it closes, and silence keeps it asking rather than switching the station off. The station asks about once a
/// second, so an answer is built only when the print of what it is drawn from changed; an unchanged one is not built
/// at all, and a fresh ask gets the last answer again.
/// </summary>
public sealed class StationFeed
{
    private string? _last;
    private ulong _lastPrint;
    private bool _isPrintKnown;

    /// <summary>
    /// True when the last answer was built from this same print, so nothing needs building; resend is that answer when
    /// the page asked afresh, and null when it already holds it.
    /// </summary>
    public bool IsUnchanged(ulong print, bool isFresh, out string? resend)
    {
        resend = null;

        if (!_isPrintKnown || _last == null || print != _lastPrint) return false;

        if (isFresh) resend = _last;

        return true;
    }

    /// <summary>
    /// The answer to send, or null to send nothing. json is null when the basket screen is not open. print is what the
    /// answer was built from, so the next unchanged ask can skip the build; without one the next ask builds again.
    /// </summary>
    public string? Next(string? json, bool isFresh, ulong? print = null)
    {
        if (json == null) return null;

        _isPrintKnown = print.HasValue;
        _lastPrint = print ?? 0;

        if (json == _last && !isFresh) return null;

        _last = json;

        return json;
    }

    /// <summary>The next answer is built and goes out even if it matches the last one.</summary>
    public void Forget()
    {
        _last = null;
        _isPrintKnown = false;
    }
}

/// <summary>When a fill that placed whole stacks can move on: once they are all in the basket, or once the wait runs out.</summary>
public static class FillWait
{
    public static bool IsDone(IReadOnlyList<long> placed, ISet<long> held, float now, float deadline)
    {
        if (now >= deadline) return true;

        for (var i = 0; i < placed.Count; i++)
        {
            if (!held.Contains(placed[i])) return false;
        }

        return true;
    }
}
