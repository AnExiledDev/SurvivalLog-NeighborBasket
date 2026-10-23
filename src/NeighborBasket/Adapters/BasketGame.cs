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

/// <summary>What the player holds that could go in the basket, what is already in it, and how many cells are left.</summary>
internal sealed record BasketStock(List<Candidate> Sources, HashSet<int> CategoriesInBasket, int FreeCells);

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

        var sources = new List<Candidate>();
        var owners = new List<long>();

        if (agents.GetLeadingRole() is { } player) owners.Add(player.InstanceId);

        foreach (var furniture in HomeFurnitures(agents)) owners.Add(furniture.InstanceId);

        foreach (var ownerId in owners)
        {
            foreach (var item in Held(items, ownerId)) sources.Add(CandidateOf(config, item, ownerId));
        }

        var inBasket = new HashSet<int>();
        var used = 0;

        foreach (var item in Held(items, basketId))
        {
            used++;

            if (config.Get_Config_Item(item.ItemConfigId) is { } row) inBasket.Add(row.Category);
        }

        return new BasketStock(sources, inBasket, MaxCells(config) - used);
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

    private static Candidate CandidateOf(ConfigManager config, ItemData item, long ownerId)
    {
        var row = config.Get_Config_Item(item.ItemConfigId);

        return new Candidate(item.InstanceId, ownerId, item.ItemConfigId, row?.Category ?? 0, row?.TradeValue ?? 0, item.ItemCount, NeighborRules.IsBasketForbidItem(item.ItemConfigId));
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
