using System.Collections.Generic;
using System.Linq;

namespace NeighborBasket.Core;

/// <summary>
/// Which basket station answers go to the page. The page keeps the last answer, so one equal to it is skipped, unless
/// the page asks afresh because it holds nothing (it just opened or reloaded) or already drew a move it is waiting to
/// hear back on. While the basket screen is not open nothing is sent: the page can still think it is open for a moment
/// as it closes, and silence keeps it asking rather than switching the station off.
/// </summary>
public sealed class StationFeed
{
    private string? _last;

    /// <summary>The answer to send, or null to send nothing. json is null when the basket screen is not open.</summary>
    public string? Next(string? json, bool isFresh)
    {
        if (json == null || (json == _last && !isFresh)) return null;

        _last = json;

        return json;
    }

    /// <summary>The next answer goes out even if it matches the last one.</summary>
    public void Forget() => _last = null;
}

/// <summary>When a fill that placed whole stacks can move on: once they are all in the basket, or once the wait runs out.</summary>
public static class FillWait
{
    public static bool IsDone(IEnumerable<long> placed, ISet<long> held, float now, float deadline) => now >= deadline || placed.All(held.Contains);
}
