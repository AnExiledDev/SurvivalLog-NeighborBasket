using System;
using GameCore.HotUpdate;

namespace NeighborBasket.Adapters;

/// <summary>Asks the game which language it is showing, only once the game has made its settings. Anything that goes wrong reads as English.</summary>
internal static class GameLanguage
{
    public static bool IsChinese()
    {
        try
        {
            return GameSingleton.Existing<SettingManager>()?.IsChinese() == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The game's language setting, for keeping names read from its tables only while it stays the same; empty when unknown.</summary>
    public static string Key()
    {
        try
        {
            return GameSingleton.Existing<SettingManager>()?.GetLanguageKey() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
