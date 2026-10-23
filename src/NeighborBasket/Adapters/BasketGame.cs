using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>The open basket screen: the basket's owner id, her wish as the screen shows it, and the names it gives each wished category.</summary>
internal sealed record BasketScreen(long BasketId, BasketWish Wish, Dictionary<int, string> Names);

/// <summary>
/// What the player holds that could go in the basket, what is already in it, and the cells and weight left. WeightNote
/// says why the weight could not be read (the fill then ignores weight and the game refuses what does not fit).
/// </summary>
internal sealed record BasketStock(List<Candidate> Sources, HashSet<int> CategoriesInBasket, int FreeCells, float FreeWeight, string? WeightNote);

/// <summary>The only code that reads or moves the game's items, or calls the basket back.</summary>
internal sealed class BasketGame
{
    private const int DefaultMaxCells = 5;

    /// <summary>The bag screen while it shows the neighbor's basket on its right side; null otherwise.</summary>
    public BasketScreen? OpenBasket()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screenType = Il2CppType.Of<WebUI_BackpackUI>();

        if (ui == null || !ui.IsUIActive(screenType)) return null;

        var screen = ui.GetStateByUI(screenType)?.TryCast<State_Web_BackpackUI>();
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;

        if (screen == null || agents == null || !screen.IsDualBag) return null;
        if (!IsBasket(agents.GetAgent(screen.OwnerId_B)?.TryCast<Furniture>())) return null;

        var names = new Dictionary<int, string>();
        var wish = WishOf(screen.NeighborWishSnap, names);

        return new BasketScreen(screen.OwnerId_B, wish, names);
    }

    /// <summary>Every stack in home storage and in the player's bag, with the basket's own contents and free cells.</summary>
    public BasketStock? ReadStock(long basketId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var items = world?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (agents == null || items == null || config == null) return null;

        var weigh = true;
        string? weightNote = null;
        var freeWeight = float.PositiveInfinity;

        try
        {
            freeWeight = FreeWeight(items, basketId);
        }
        catch (Exception ex)
        {
            weigh = false;
            weightNote = ex.Message;
        }

        var sources = new List<Candidate>();
        var owners = new List<long>();

        if (agents.GetLeadingRole() is { } player) owners.Add(player.InstanceId);

        foreach (var furniture in HomeFurnitures(agents)) owners.Add(furniture.InstanceId);

        foreach (var ownerId in owners)
        {
            foreach (var item in Held(items, ownerId)) sources.Add(CandidateOf(config, item, ownerId, weigh ? items.GetItemActualWeight(item) : 0));
        }

        var inBasket = new HashSet<int>();
        var used = 0;

        foreach (var item in Held(items, basketId))
        {
            used++;

            if (config.Get_Config_Item(item.ItemConfigId) is { } row) inBasket.Add(row.Category);
        }

        return new BasketStock(sources, inBasket, MaxCells(config) - used, freeWeight, weightNote);
    }

    /// <summary>The satiety the game's rescue rule counts for what the basket holds now; null with no rescue going.</summary>
    public float? BasketSatiety(long basketId)
    {
        var held = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager?.GetItemDataList(basketId);

        return held == null ? null : NeedReader.Satiety(held);
    }

    /// <summary>The basket's owner id in the home, whether or not its screen is open; 0 when there is none.</summary>
    public long HomeBasketId()
    {
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;

        if (agents == null || NeighborRules.GetBasketFurnitureId() <= 0) return 0;

        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);

        for (var i = 0; found != null && i < found.Count; i++)
        {
            if (IsBasket(found[i])) return found[i].InstanceId;
        }

        return 0;
    }

    /// <summary>What the basket holds, as "name x count" in the game's language, in a stable order.</summary>
    public string BasketContents(long basketId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (items == null || config == null || basketId == 0) return "";

        var counts = new SortedDictionary<int, int>();

        foreach (var item in Held(items, basketId)) counts[item.ItemConfigId] = (counts.TryGetValue(item.ItemConfigId, out var count) ? count : 0) + item.ItemCount;

        var parts = new List<string>();

        foreach (var entry in counts) parts.Add(ItemName(config, entry.Key) + " x" + entry.Value);

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Puts a pick in the basket with the game's own quick-move, which also redraws both bags. One unit needs
    /// Stacking; when it skips a single item the whole (one-item) stack moves instead.
    /// </summary>
    public bool Move(Pick pick, long basketId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

        if (items == null) return false;

        if (pick.IsOneUnit && pick.Item.Count > 1)
        {
            return StackingUnits.MoveOne(pick.Item.ItemId, pick.Item.OwnerId, basketId) == true;
        }

        return items.TryQuickMoveItem(pick.Item.ItemId, pick.Item.OwnerId, basketId, true, false);
    }

    /// <summary>Whether the basket is out with her now, by the game's own return schedule.</summary>
    public bool IsBasketAway() => GameSingleton.Existing<BattleLogicWorld>()?._NeighborBasketReturnManager?.IsBasketBusy() == true;

    /// <summary>The game's own early return, the one its debug menu uses: the basket comes home with whatever she sends.</summary>
    public bool CallBack()
    {
        var returns = GameSingleton.Existing<BattleLogicWorld>()?._NeighborBasketReturnManager;

        if (returns == null || !returns.IsBasketBusy()) return false;

        returns.ForceReturnNow();

        return true;
    }

    private static Candidate CandidateOf(ConfigManager config, ItemData item, long ownerId, int weight)
    {
        var row = config.Get_Config_Item(item.ItemConfigId);

        return new Candidate(item.InstanceId, ownerId, item.ItemConfigId, row?.Category ?? 0, row?.TradeValue ?? 0, item.ItemCount, NeighborRules.IsBasketForbidItem(item.ItemConfigId), Math.Max(0, weight));
    }

    /// <summary>
    /// The weight the basket still takes, by the same numbers the game's quick-move checks: its max weight less what it
    /// holds. Infinite when the game does not weigh the basket.
    /// </summary>
    private static float FreeWeight(ItemManager items, long basketId)
    {
        if (!items.TryGetTargetBagConfig(basketId, out _, out var maxWeight, out var checkBurden) || !checkBurden) return float.PositiveInfinity;

        return Math.Max(0f, maxWeight - items.GetBagCurrentWeight(basketId));
    }

    private static string ItemName(ConfigManager config, int configId)
    {
        var row = config.Get_Config_Item(configId);

        if (row == null) return "#" + configId;

        var key = row.ItemName;
        var text = string.IsNullOrEmpty(key) ? null : config.GetLocalTxt(key);

        if (!string.IsNullOrEmpty(text) && text != key) return text!;
        if (!string.IsNullOrEmpty(row.ItemName_Local)) return row.ItemName_Local;

        return string.IsNullOrEmpty(key) ? "#" + configId : key!;
    }

    /// <summary>The game's own cell count for the basket, as its bag screen reads it.</summary>
    private static int MaxCells(ConfigManager config) => config.Get_Config_GlobalSetting("Neighbor_BasketMaxItems")?.Params1 is { } cells && cells > 0 ? cells : DefaultMaxCells;

    /// <summary>Home furniture with a bag that counts as the player's storage, without the basket itself.</summary>
    private static List<Furniture> HomeFurnitures(AgentManager agents)
    {
        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);
        var homes = new List<Furniture>();

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture != null && !ItemManager.IsExcludedFromCostPool(furniture.InstanceId) && !IsBasket(furniture)) homes.Add(furniture);
        }

        return homes;
    }

    private static bool IsBasket(Furniture? furniture)
    {
        var basketId = NeighborRules.GetBasketFurnitureId();

        return basketId > 0 && furniture != null && furniture.AgentConfigId == basketId;
    }

    private static IEnumerable<ItemData> Held(ItemManager items, long ownerId)
    {
        var held = items.GetItemDataList(ownerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];

            // A pooled item can sit in a list it no longer belongs to; the game would refuse to move it.
            if (item != null && item.OwnerId == ownerId && item.ItemCount > 0) yield return item;
        }
    }

    private static BasketWish WishOf(NeighborWishSnapshot? snap, Dictionary<int, string> names)
    {
        if (snap == null) return BasketWish.None;

        var categories = new List<int>();

        for (var i = 0; snap.wishCats != null && i < snap.wishCats.Count; i++)
        {
            categories.Add(snap.wishCats[i]);

            if (snap.wishNames != null && i < snap.wishNames.Count) names[snap.wishCats[i]] = snap.wishNames[i];
        }

        if (snap.crisisActive && !string.IsNullOrEmpty(snap.crisisCatName)) names[snap.crisisCat] = snap.crisisCatName;

        return new BasketWish(snap.active, snap.wishPending, snap.basketAway, snap.dying, categories, snap.crisisActive ? snap.crisisCat : null);
    }
}
