using System;
using System.Collections.Concurrent;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// Copies each of the neighbor's thoughts (the lines the game floats over the screen) into a queue for the gift log.
/// The game shows them as before; a failure here only loses the copy.
/// </summary>
internal static class MonologueHook
{
    private static readonly ConcurrentQueue<string> Said = new();

    public static void Apply(Harmony harmony, Action<string> log)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(NeighborMonologueManager), nameof(NeighborMonologueManager.PushMonologue)), prefix: new HarmonyMethod(typeof(MonologueHook), nameof(Copy)));
            log("listening for her thoughts");
        }
        catch (Exception ex)
        {
            log(HookFailure.Prefix + " her thoughts, the gift log will not list them: " + ex.Message);
        }
    }

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
