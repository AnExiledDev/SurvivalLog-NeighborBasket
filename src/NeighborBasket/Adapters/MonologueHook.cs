using System;
using System.Collections.Concurrent;
using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Logic;

namespace NeighborBasket.Adapters;

/// <summary>
/// Copies each of the neighbor's thoughts (the lines the game floats over the screen) into a queue for the gift log.
/// The game shows them as before; a failure here only loses the copy.
/// </summary>
internal static class MonologueHook
{
    private static readonly ConcurrentQueue<string> Said = new();

    public static void Apply(PatchKit hooks) =>
        hooks.Patch(typeof(MonologueHook), typeof(NeighborMonologueManager), nameof(NeighborMonologueManager.PushMonologue), prefix: nameof(Copy),
            done: "listening for her thoughts", consequence: "the gift log will not list them");

    public static bool TryTake(out string text) => Said.TryDequeue(out text!);

    private static void Copy(string text)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(text) && Said.Count < 100) Said.Enqueue(text.Trim());
        }
        catch (Exception)
        {
            // Never in the game's way.
        }
    }
}
