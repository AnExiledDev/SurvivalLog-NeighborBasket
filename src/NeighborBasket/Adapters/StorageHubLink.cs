using System;
using System.Linq;
using System.Reflection;

namespace NeighborBasket.Adapters;

/// <summary>
/// Storage Hub's put-away for the basket, found by name so this mod loads and runs without Storage Hub. The method is
/// StorageHub.BasketApi.PutAwayBasket(): it returns the line to show, or null when Storage Hub has no game session yet.
/// </summary>
internal static class StorageHubLink
{
    public const string Guid = "com.dev.survivallog.storagehub";

    private static readonly Lazy<MethodInfo?> PutAway = new(Find);

    public static bool IsAvailable => PutAway.Value != null;

    /// <summary>Puts the basket away through Storage Hub; null when Storage Hub is missing or not ready.</summary>
    public static string? PutAwayBasket() => PutAway.Value?.Invoke(null, null) as string;

    private static MethodInfo? Find()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => candidate.GetName().Name == "StorageHub");
        var method = assembly?.GetType("StorageHub.BasketApi")?.GetMethod("PutAwayBasket", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        return method?.ReturnType == typeof(string) ? method : null;
    }
}
