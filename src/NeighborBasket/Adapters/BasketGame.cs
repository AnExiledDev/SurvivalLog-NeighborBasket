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
/// The station's data, with the categories already in the basket. WeightNote says why the weight could not be read
/// (the fill then ignores weight and the game refuses what does not fit).
/// </summary>
internal sealed record BasketStock(StationData Station, HashSet<int> CategoriesInBasket, string? WeightNote);

/// <summary>The only code that reads or moves the game's items, or calls the basket back.</summary>
internal sealed class BasketGame
{
    // The basket's grid by its bag config (5 x 3), for when the game cannot say.
    private const int DefaultCols = 5;
    private const int DefaultRows = 3;

    private readonly Dictionary<int, bool> _breakable = new();
    private readonly Action<string> _log;

    public BasketGame(Action<string> log) => _log = log;

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

    /// <summary>
    /// Every stack in the player's bag and in home storage, the basket's own contents with their cells, its grid and
    /// weight, and each place by name: the bag first, then each storage. Values follow the game's own send scoring.
    /// </summary>
    public BasketStock? ReadStock(long basketId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var items = world?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (agents == null || items == null || config == null) return null;

        string? weightNote = null;
        var maxWeight = 0f;
        var usedWeight = 0f;
        var cols = DefaultCols;
        var rows = DefaultRows;
        var weigh = true;

        try
        {
            if (items.TryGetTargetBagConfig(basketId, out var size, out var max, out var checkBurden))
            {
                if (size.x > 0 && size.y > 0)
                {
                    cols = size.x;
                    rows = size.y;
                }

                if (checkBurden && max > 0)
                {
                    maxWeight = max;
                    usedWeight = items.GetBagCurrentWeight(basketId);
                }
            }
        }
        catch (Exception ex)
        {
            weigh = false;
            weightNote = ex.Message;
        }

        var places = new List<Place>();
        var sources = new List<StationItem>();
        var bagId = 0L;

        if (agents.GetLeadingRole() is { } player)
        {
            bagId = player.InstanceId;
            places.Add(new Place(bagId, ""));
        }

        foreach (var furniture in HomeFurnitures(agents)) places.Add(new Place(furniture.InstanceId, FurnitureName(config, furniture)));

        foreach (var place in places)
        {
            foreach (var item in Held(items, place.Id)) sources.Add(StationItemOf(config, items, item, place.Id, weigh));
        }

        var inBasket = new List<StationItem>();
        var categories = new HashSet<int>();

        foreach (var item in Held(items, basketId))
        {
            var station = StationItemOf(config, items, item, basketId, weigh) with { Breaks = IsBreakable(config, item.ItemConfigId) };

            inBasket.Add(station);
            categories.Add(station.Category);
        }

        return new BasketStock(new StationData(cols, rows, maxWeight, usedWeight, inBasket, sources, places, bagId), categories, weightNote);
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
    /// Puts a planned stack in the basket at its planned cell, with the same move the game makes when the player drags
    /// an item there. The game runs it a moment later and checks room and weight itself, so the caller looks again
    /// afterwards to see what went in.
    /// </summary>
    public static void PlaceAt(Pick pick, long basketId) => Ac_Item_ChangePos.SendAction(pick.Item.ItemId, pick.Item.OwnerId, basketId, new UnityEngine.Vector2Int(pick.X, pick.Y));

    /// <summary>The ids of the stacks the basket holds now.</summary>
    public HashSet<long> BasketItemIds(long basketId)
    {
        var ids = new HashSet<long>();
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

        if (items == null) return ids;

        foreach (var item in Held(items, basketId)) ids.Add(item.InstanceId);

        return ids;
    }

    /// <summary>
    /// Moves one item of a stack, or the whole stack, between two bags with the game's own quick-move, which finds the
    /// free cells and redraws both bags. One item needs Stacking; without it the whole stack moves.
    /// </summary>
    public bool Move(long itemId, long fromOwnerId, long toOwnerId, bool isWholeStack)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var item = items?.GetItemData(itemId);

        if (items == null || item == null || item.OwnerId != fromOwnerId) return false;

        if (!isWholeStack && item.ItemCount > 1 && StackingUnits.MoveOne(itemId, fromOwnerId, toOwnerId) is { } moved) return moved;

        return items.TryQuickMoveItem(itemId, fromOwnerId, toOwnerId, true, false);
    }

    /// <summary>
    /// Moves this many items of a stack into another bag one at a time with Stacking's unit move, never the whole stack;
    /// returns how many moved. Stops when the stack is gone, has one item left, or Stacking refuses.
    /// </summary>
    public int MoveUnits(long itemId, long fromOwnerId, long toOwnerId, int count) =>
        UnitSteps.Move(itemId, count, id => MoveOneUnit(id, fromOwnerId, toOwnerId));

    private static UnitStep MoveOneUnit(long itemId, long fromOwnerId, long toOwnerId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var item = items?.GetItemData(itemId);

        if (items == null || item == null || item.OwnerId != fromOwnerId || item.ItemCount <= 1) return new UnitStep(false, null);

        var configId = item.ItemConfigId;
        var pos = item.BagPos;

        if (StackingUnits.MoveOne(itemId, fromOwnerId, toOwnerId) != true) return new UnitStep(false, null);

        return new UnitStep(true, RestOf(items, itemId, fromOwnerId, configId, pos));
    }

    /// <summary>
    /// The stack left behind after one unit moved: the same item if it stayed, otherwise the stack of the same kind
    /// Stacking put back on the cell it stood on (it moves the stack's own item and leaves the rest under a new id).
    /// </summary>
    private static long? RestOf(ItemManager items, long itemId, long ownerId, int configId, UnityEngine.Vector2Int pos)
    {
        if (items.GetItemData(itemId) is { } same && same.OwnerId == ownerId) return itemId;

        foreach (var other in Held(items, ownerId))
        {
            if (other.ItemConfigId == configId && other.BagPos.x == pos.x && other.BagPos.y == pos.y) return other.InstanceId;
        }

        return null;
    }

    /// <summary>The owner, config and name of a stack, by its id; null when it is gone.</summary>
    public (long OwnerId, int ConfigId, string Name)? Find(long itemId)
    {
        var item = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager?.GetItemData(itemId);
        var config = GameSingleton.Existing<ConfigManager>();

        if (item == null || config == null || item.ItemCount <= 0) return null;

        return (item.OwnerId, item.ItemConfigId, ItemName(config, item.ItemConfigId));
    }

    /// <summary>
    /// Whether the game's own Use takes this item apart into materials and nothing else (see Breakdown). Read once per
    /// kind from the item, effect and package tables; a kind whose tables cannot be read does not break down.
    /// </summary>
    public bool IsBreakable(ConfigManager config, int configId)
    {
        if (_breakable.TryGetValue(configId, out var known)) return known;

        var result = false;

        try
        {
            result = Breakdown.IsBreakable(ReadUse(config, configId));
        }
        catch (Exception ex)
        {
            _log($"break-down check for config {configId} failed: {ex.Message}");
        }

        _breakable[configId] = result;

        return result;
    }

    private static UseInfo? ReadUse(ConfigManager config, int configId)
    {
        var item = config.Get_Config_Item(configId);

        if (item == null || item.UseAction <= 0) return null;

        var effect = config.Get_Config_Effect(item.UseAction);

        if (effect == null) return null;

        var hasOther = effect.ItemSatietyStart != 0 || effect.ItemMoraleStart != 0 || effect.ItemStaminaStart != 0 || effect.ItemHealthStart != 0 || effect.ItemVitalityStart != 0
            || effect.ItemSatietyEnd != 0 || effect.ItemMoraleEnd != 0 || effect.ItemStaminaEnd != 0 || effect.ItemHealthEnd != 0 || effect.ItemVitalityEnd != 0
            || effect.ItemEndBuff01 != 0 || effect.ItemEndBuff02 != 0 || (effect.EAddBuff?.Count ?? 0) > 0 || (effect.EDelBuff?.Count ?? 0) > 0 || effect.PonitEnd != 0;
        var isRandom = false;
        var outputs = new List<int>();
        var packages = effect.ItemGetEnd;

        for (var i = 0; packages != null && i < packages.Count; i++)
        {
            var package = config.Get_Config_Package(packages[i]);

            if (package == null) return null;

            if ((package.RandomID?.Count ?? 0) > 0) isRandom = true;

            for (var j = 0; package.ItemIdList != null && j < package.ItemIdList.Count; j++) outputs.Add(config.Get_Config_Item(package.ItemIdList[j])?.Category ?? 0);
        }

        return new UseInfo(item.CantUse, item.UseAction, hasOther, isRandom, outputs, item.StackLimit);
    }

    /// <summary>
    /// Lines up one hand break-down of an item in the bag, the way the bag's own Use does
    /// (BattleLogicWorld.OnUseItemFromFurnitureRequest with no furniture: the item's use action aimed at the item), after
    /// the game's own use check. Nothing starts until StartLine.
    /// </summary>
    public bool LineUpBreakdown(long itemId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();
        var config = GameSingleton.Existing<ConfigManager>();
        var item = items?.GetItemData(itemId);

        if (items == null || role == null || config == null || item == null) return false;

        var actionId = config.Get_Config_Item(item.ItemConfigId)?.UseAction ?? 0;

        if (actionId <= 0 || !IsBreakable(config, item.ItemConfigId)) return false;

        if (!items.VerifyItemUse(itemId, item.ItemConfigId, out var failMessage))
        {
            _log($"break down: item {itemId} (config {item.ItemConfigId}) refused by the use check: {failMessage}");

            return false;
        }

        role.AddLeadingRoleAction(actionId, itemId, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);

        return true;
    }

    /// <summary>
    /// Clears the character's not-yet-started action line, as the game does before a Use: only when what the character
    /// is doing now may be interrupted.
    /// </summary>
    public void ClearLine()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var role = world?._AgentManager?.GetLeadingRole();

        if (world == null || role == null) return;

        try
        {
            if (!world.IsCurrentBehaviorInterruptible(role)) return;
        }
        catch (Exception ex)
        {
            _log($"break down: the interrupt check failed, the line is kept: {ex.Message}");

            return;
        }

        role.ClearLeadingRoleActionCache();
    }

    /// <summary>Starts the lined-up actions as one batch; on failure the line is cleared so nothing is left stranded.</summary>
    public bool StartLine()
    {
        var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (role == null) return false;

        try
        {
            role.DoLeadingRoleActionCache();

            return true;
        }
        catch (Exception ex)
        {
            _log($"break down: starting the line failed: {ex}");
            role.ClearLeadingRoleActionCache();

            return false;
        }
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

    /// <summary>
    /// A stack as the station and the fill see it. Its value is what the game counts when she scores the basket: count
    /// times trade value times the uses it still has, with fuel scaled the way the neighbor rule scales it.
    /// </summary>
    private static StationItem StationItemOf(ConfigManager config, ItemManager items, ItemData item, long ownerId, bool weigh)
    {
        var row = config.Get_Config_Item(item.ItemConfigId);
        var category = row?.Category ?? 0;
        var value = 0;

        if (row != null)
        {
            var raw = item.ItemCount * row.TradeValue * ItemValueDisplayHelper.GetEffectiveTradeUses(item, row);

            value = NeighborRules.ApplyFuelValueScale(category, (int)Math.Round(raw));
        }

        var size = item.ItemSize;
        var pos = item.BagPos;

        return new StationItem(item.InstanceId, ownerId, item.ItemConfigId, ItemName(config, item.ItemConfigId), row?.WebIcon ?? "", category, Math.Max(0, value), item.ItemCount,
            weigh ? Math.Max(0, items.GetItemActualWeight(item)) : 0, Math.Max(1, size.x), Math.Max(1, size.y), NeighborRules.IsBasketForbidItem(item.ItemConfigId), pos.x, pos.y);
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

    private static string FurnitureName(ConfigManager config, Furniture furniture)
    {
        var key = config.Get_Config_Furniture(furniture.AgentConfigId)?.Name;
        var text = string.IsNullOrEmpty(key) ? null : config.GetLocalTxt(key);

        if (!string.IsNullOrEmpty(text) && text != key) return text!;

        return string.IsNullOrWhiteSpace(key) ? "#" + furniture.InstanceId : key!;
    }

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

        return new BasketWish(snap.active, snap.wishPending, snap.basketAway, snap.dying, categories, snap.crisisActive ? snap.crisisCat : null, snap.max > 0 ? Math.Max(0, snap.max - snap.progress) : 0);
    }
}
