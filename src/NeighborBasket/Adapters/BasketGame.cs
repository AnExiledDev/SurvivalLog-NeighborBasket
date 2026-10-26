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
/// (the fill then ignores weight and the game refuses what does not fit). Print changes whenever anything the station
/// shows from it changes.
/// </summary>
internal sealed record BasketStock(StationData Station, HashSet<int> CategoriesInBasket, string? WeightNote, ulong Print);

/// <summary>
/// The only code that reads or moves the game's items, or calls the basket back. The station reads every stack at home
/// about once a second, so stacks are read straight from the game's memory (no interop object per stack), and a stack
/// is only made into a station row again when something about it changed. If that memory is ever not laid out as
/// expected, reads go through the game's objects instead, and the log says so once.
/// </summary>
internal sealed class BasketGame
{
    // The basket's grid by its bag config (5 x 3), for when the game cannot say.
    private const int DefaultCols = 5;
    private const int DefaultRows = 3;

    private static Il2CppSystem.Type? _screenType;

    private readonly Dictionary<int, bool> _breakable = new();
    private readonly Dictionary<int, Kind> _kinds = new();
    private readonly Dictionary<int, string?> _furnitureNames = new();
    private readonly Dictionary<long, Place> _places = new();
    private readonly Dictionary<long, Row> _rows = new();
    private readonly Action<string> _log;
    private Il2CppSystem.Collections.Generic.Dictionary<int, Config_Item>? _itemTable;
    private IntPtr _configPointer;
    private string _language = "";
    // Bumped whenever what the rows were made from changes (a config reload or a language switch), so a print taken
    // before cannot match one taken after.
    private int _generation;
    private int _pass;

    // What one read of the stock found, kept between reads so a read allocates nothing when nothing changed.
    private readonly List<Stamp> _stamps = new();
    private readonly List<Stamp> _basketStamps = new();
    private readonly List<(long Id, int ConfigId)> _found = new();
    private BasketStock? _lastStock;

    // The memory reader, learned at the first read; off for good after anything unexpected.
    private bool _hasLearned;
    private bool _isDirect;
    private RawList? _itemList;
    private RawList? _furnitureList;
    private IntPtr _itemClass;
    private int _instanceIdAt;
    private int _ownerIdAt;
    private int _configIdAt;
    private int _countAt;
    private int _sizeAt;
    private int _posAt;
    private int _useTimesAt;
    private int _maxUseTimesAt;
    private int _instanceWeightAt;
    private int _agentConfigIdAt;

    // The basket's contents as the gift log writes them, kept until the basket changes.
    private ulong _contentsPrint;
    private string _contents = "";

    /// <summary>What every stack of one item kind shares: its table row, name, icon, category and whether she refuses it.</summary>
    private sealed record Kind(Config_Item? Row, string Name, string Icon, int Category, bool IsForbidden);

    /// <summary>
    /// One stack as read this pass. Pointer is only good for the frame it was read in. Size and position are cells.
    /// </summary>
    private struct Stamp
    {
        public IntPtr Pointer;
        public long Id;
        public long OwnerId;
        public int ConfigId;
        public int Count;
        public int UseTimes;
        public int MaxUseTimes;
        public int InstanceWeight;
        public int Width;
        public int Height;
        public int X;
        public int Y;

        public bool Equals(in Stamp other) =>
            Id == other.Id && OwnerId == other.OwnerId && ConfigId == other.ConfigId && Count == other.Count && UseTimes == other.UseTimes && MaxUseTimes == other.MaxUseTimes
            && InstanceWeight == other.InstanceWeight && Width == other.Width && Height == other.Height && X == other.X && Y == other.Y;

        public ulong MixInto(ulong print)
        {
            print = Core.Print.Mix(print, Id);
            print = Core.Print.Mix(print, OwnerId);
            print = Core.Print.Mix(print, ConfigId);
            print = Core.Print.Mix(print, Count);
            print = Core.Print.Mix(print, UseTimes);
            print = Core.Print.Mix(print, MaxUseTimes);
            print = Core.Print.Mix(print, InstanceWeight);
            print = Core.Print.Mix(print, Width);
            print = Core.Print.Mix(print, Height);
            print = Core.Print.Mix(print, X);

            return Core.Print.Mix(print, Y);
        }
    }

    /// <summary>A stack made into a station row, with what it was made from and the last read that saw it.</summary>
    private sealed class Row
    {
        public Stamp Stamp;
        public bool IsInBasket;
        public bool Weigh;
        public StationItem Item = null!;
        public int SeenAt;
    }

    public BasketGame(Action<string> log) => _log = log;

    /// <summary>
    /// The game's language setting. Item and storage names are read in it, so a switch drops every name read so far and
    /// the next stock read builds its rows again.
    /// </summary>
    public void SetLanguage(string key)
    {
        if (key == _language) return;

        _language = key;
        ForgetRows();
    }

    /// <summary>The bag screen while it shows the neighbor's basket on its right side; null otherwise.</summary>
    public BasketScreen? OpenBasket()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screenType = _screenType ??= Il2CppType.Of<WebUI_BackpackUI>();

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
    /// When nothing the station shows changed since the last read, that read's answer comes back as it was.
    /// </summary>
    public BasketStock? ReadStock(long basketId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var items = world?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (agents == null || items == null || config == null) return null;

        FollowConfig(config);

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

        var bagId = 0L;

        _found.Clear();

        if (agents.GetLeadingRole() is { } player) bagId = player.InstanceId;

        FindHomeStorage(agents, _found);

        var print = Core.Print.Start;

        print = Core.Print.Mix(print, _generation);
        print = Core.Print.Mix(print, basketId);
        print = Core.Print.Mix(print, cols);
        print = Core.Print.Mix(print, rows);
        print = Core.Print.Mix(print, maxWeight);
        print = Core.Print.Mix(print, usedWeight);
        print = Core.Print.Mix(print, weigh);
        print = Core.Print.Mix(print, weightNote);
        print = Core.Print.Mix(print, bagId);

        _stamps.Clear();
        _basketStamps.Clear();

        if (bagId != 0) ReadHeld(items, bagId, _stamps);

        foreach (var (id, _) in _found)
        {
            print = Core.Print.Mix(print, id);
            ReadHeld(items, id, _stamps);
        }

        ReadHeld(items, basketId, _basketStamps);

        foreach (var stamp in _stamps) print = stamp.MixInto(print);

        print = Core.Print.Mix(print, -1L);

        foreach (var stamp in _basketStamps) print = stamp.MixInto(print);

        if (_lastStock is { } last && last.Print == print) return last;

        _pass++;

        var places = new List<Place>(_found.Count + 1);
        var sources = new List<StationItem>(_stamps.Count);
        var inBasket = new List<StationItem>(_basketStamps.Count);
        var categories = new HashSet<int>();

        if (bagId != 0) places.Add(PlaceOf(config, bagId, -1));

        foreach (var (id, configId) in _found) places.Add(PlaceOf(config, id, configId));

        foreach (var stamp in _stamps) sources.Add(RowOf(config, stamp, false, weigh));

        foreach (var stamp in _basketStamps)
        {
            var item = RowOf(config, stamp, true, weigh);

            inBasket.Add(item);
            categories.Add(item.Category);
        }

        Prune();

        return _lastStock = new BasketStock(new StationData(cols, rows, maxWeight, usedWeight, inBasket, sources, places, bagId), categories, weightNote, print);
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
        var basketConfigId = NeighborRules.GetBasketFurnitureId();

        if (agents == null || basketConfigId <= 0) return 0;

        _found.Clear();
        FindFurniture(agents, _found, false);

        foreach (var (id, configId) in _found)
        {
            if (configId == basketConfigId) return id;
        }

        return 0;
    }

    /// <summary>
    /// What the basket holds, as "name x count" in the game's language, in a stable order. Read every second for the
    /// gift log, so the text is only made again when the basket's stacks change.
    /// </summary>
    public string BasketContents(long basketId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (items == null || config == null || basketId == 0) return "";

        FollowConfig(config);
        _basketStamps.Clear();
        ReadHeld(items, basketId, _basketStamps);

        var print = Core.Print.Mix(Core.Print.Start, _generation);

        foreach (var stamp in _basketStamps)
        {
            print = Core.Print.Mix(print, stamp.ConfigId);
            print = Core.Print.Mix(print, stamp.Count);
        }

        if (print == _contentsPrint) return _contents;

        var counts = new SortedDictionary<int, int>();

        foreach (var stamp in _basketStamps) counts[stamp.ConfigId] = (counts.TryGetValue(stamp.ConfigId, out var count) ? count : 0) + stamp.Count;

        var parts = new List<string>();

        foreach (var entry in counts) parts.Add(KindOf(entry.Key).Name + " x" + entry.Value);

        _contentsPrint = print;

        return _contents = string.Join(", ", parts);
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

        FollowConfig(config);

        return (item.OwnerId, item.ItemConfigId, KindOf(item.ItemConfigId).Name);
    }

    /// <summary>The player's own bag; 0 when there is no player.</summary>
    public long BagId() => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole()?.InstanceId ?? 0;

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
    /// The station row for a stack: the one made before when nothing about the stack changed, otherwise a new one. Its
    /// value is what the game counts when she scores the basket: count times trade value times the uses it still has,
    /// with fuel scaled the way the neighbor rule scales it. Its weight is the game's own (ItemManager.GetItemActualWeight
    /// is this same rule on the item's row).
    /// </summary>
    private StationItem RowOf(ConfigManager config, in Stamp stamp, bool isInBasket, bool weigh)
    {
        if (_rows.TryGetValue(stamp.Id, out var known) && known.IsInBasket == isInBasket && known.Weigh == weigh && known.Stamp.Equals(stamp))
        {
            known.SeenAt = _pass;

            return known.Item;
        }

        var kind = KindOf(stamp.ConfigId);
        var row = kind.Row;
        var category = kind.Category;
        var value = 0;
        var weight = 0;

        if (row != null)
        {
            var raw = stamp.Count * row.TradeValue * ItemValueDisplayHelper.GetEffectiveTradeUses(new ItemData(stamp.Pointer), row);

            value = NeighborRules.ApplyFuelValueScale(category, (int)Math.Round(raw));

            if (weigh) weight = Math.Max(0, ItemWeightRules.GetActualWeight(row, stamp.Count, stamp.UseTimes, stamp.InstanceWeight));
        }

        var item = new StationItem(stamp.Id, stamp.OwnerId, stamp.ConfigId, kind.Name, kind.Icon, category, Math.Max(0, value), stamp.Count,
            weight, Math.Max(1, stamp.Width), Math.Max(1, stamp.Height), kind.IsForbidden, stamp.X, stamp.Y, isInBasket && IsBreakable(config, stamp.ConfigId));

        _rows[stamp.Id] = new Row { Stamp = stamp, IsInBasket = isInBasket, Weigh = weigh, Item = item, SeenAt = _pass };

        return item;
    }

    private readonly List<long> _gone = new();

    /// <summary>Drops the rows of stacks this read did not find: used, sold, merged or gone from home.</summary>
    private void Prune()
    {
        _gone.Clear();

        foreach (var entry in _rows)
        {
            if (entry.Value.SeenAt != _pass) _gone.Add(entry.Key);
        }

        foreach (var id in _gone) _rows.Remove(id);
    }

    /// <summary>A place by its owner id and name; the bag (configId -1) has no name.</summary>
    private Place PlaceOf(ConfigManager config, long id, int configId)
    {
        if (_places.TryGetValue(id, out var known)) return known;

        var place = new Place(id, configId < 0 ? "" : FurnitureName(config, id, configId));

        _places[id] = place;

        return place;
    }

    /// <summary>
    /// Starts every name and row over when the item table is a new one (the configs reloaded) and remembers the table, so
    /// a kind is looked up in it directly.
    /// </summary>
    private void FollowConfig(ConfigManager config)
    {
        if (config.Pointer == _configPointer && _itemTable != null) return;

        _configPointer = config.Pointer;
        _itemTable = config._Config_Item_Dict;
        _breakable.Clear();
        ForgetRows();
    }

    private void ForgetRows()
    {
        _kinds.Clear();
        _furnitureNames.Clear();
        _places.Clear();
        _rows.Clear();
        _lastStock = null;
        _contentsPrint = 0;
        _generation++;
    }

    // The station reads a few hundred stacks each time; most share a kind, and a kind's table row and name never change
    // while the configs and the language stay the same, so each is read once (FollowConfig and SetLanguage start over).
    private Kind KindOf(int configId)
    {
        if (_kinds.TryGetValue(configId, out var known)) return known;

        var config = GameSingleton.Existing<ConfigManager>();
        Config_Item? row = null;

        if (_itemTable != null && _itemTable.TryGetValue(configId, out var found)) row = found;

        var kind = new Kind(row, config == null ? "#" + configId : ItemName(config, row, configId), row?.WebIcon ?? "", row?.Category ?? 0, NeighborRules.IsBasketForbidItem(configId));

        _kinds[configId] = kind;

        return kind;
    }

    private static string ItemName(ConfigManager config, Config_Item? row, int configId)
    {
        if (row == null) return "#" + configId;

        var key = row.ItemName;
        var text = string.IsNullOrEmpty(key) ? null : config.GetLocalTxt(key);

        if (!string.IsNullOrEmpty(text) && text != key) return text!;
        if (!string.IsNullOrEmpty(row.ItemName_Local)) return row.ItemName_Local;

        return string.IsNullOrEmpty(key) ? "#" + configId : key!;
    }

    private string FurnitureName(ConfigManager config, long instanceId, int configId)
    {
        if (!_furnitureNames.TryGetValue(configId, out var name))
        {
            var key = config.Get_Config_Furniture(configId)?.Name;
            var text = string.IsNullOrEmpty(key) ? null : config.GetLocalTxt(key);

            name = !string.IsNullOrEmpty(text) && text != key ? text : string.IsNullOrWhiteSpace(key) ? null : key;
            _furnitureNames[configId] = name;
        }

        return name ?? "#" + instanceId;
    }

    /// <summary>Home furniture with a bag that counts as the player's storage, without the basket itself.</summary>
    private void FindHomeStorage(AgentManager agents, List<(long Id, int ConfigId)> into)
    {
        FindFurniture(agents, into, true);
    }

    /// <summary>
    /// Every home furniture with a bag, by owner id and furniture kind. With isStorage, only what counts as the player's
    /// storage (the game's cost pool), without the basket.
    /// </summary>
    private void FindFurniture(AgentManager agents, List<(long Id, int ConfigId)> into, bool isStorage)
    {
        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);

        if (found == null) return;

        var basketConfigId = NeighborRules.GetBasketFurnitureId();

        Learn();

        if (_isDirect && TryFindFurnitureDirect(found.Pointer, into, isStorage, basketConfigId))
        {
            GC.KeepAlive(found);

            return;
        }

        into.Clear();

        for (var i = 0; i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture == null) continue;

            Keep(into, furniture.InstanceId, furniture.AgentConfigId, isStorage, basketConfigId);
        }
    }

    private unsafe bool TryFindFurnitureDirect(IntPtr list, List<(long Id, int ConfigId)> into, bool isStorage, int basketConfigId)
    {
        _furnitureList ??= RawList.Learn(IL2CPP.il2cpp_object_get_class(list));

        if (_furnitureList == null || !_furnitureList.TryOpen(list, out var items, out var count)) return GiveUp("the furniture list");

        for (var i = 0; i < count; i++)
        {
            var furniture = RawList.Item(items, i);

            if (furniture == IntPtr.Zero) continue;

            Keep(into, *(long*)(furniture + _instanceIdAt), *(int*)(furniture + _agentConfigIdAt), isStorage, basketConfigId);
        }

        return true;
    }

    private static void Keep(List<(long Id, int ConfigId)> into, long id, int configId, bool isStorage, int basketConfigId)
    {
        if (isStorage && ((basketConfigId > 0 && configId == basketConfigId) || ItemManager.IsExcludedFromCostPool(id))) return;

        into.Add((id, configId));
    }

    /// <summary>The live stacks one owner holds, read into stamps.</summary>
    private void ReadHeld(ItemManager items, long ownerId, List<Stamp> into)
    {
        var held = items.GetItemDataList(ownerId);

        if (held == null) return;

        Learn();

        var start = into.Count;

        if (_isDirect && TryReadHeldDirect(held.Pointer, ownerId, into))
        {
            GC.KeepAlive(held);

            return;
        }

        into.RemoveRange(start, into.Count - start);

        for (var i = 0; i < held.Count; i++)
        {
            var item = held[i];

            // A pooled item can sit in a list it no longer belongs to; the game would refuse to move it.
            if (item == null || item.OwnerId != ownerId || item.ItemCount <= 0) continue;

            var size = item.ItemSize;
            var pos = item.BagPos;

            into.Add(new Stamp
            {
                Pointer = item.Pointer, Id = item.InstanceId, OwnerId = ownerId, ConfigId = item.ItemConfigId, Count = item.ItemCount, UseTimes = item.UseTimes,
                MaxUseTimes = item.MaxUseTimes, InstanceWeight = item.InstanceWeight, Width = size.x, Height = size.y, X = pos.x, Y = pos.y,
            });
        }
    }

    private unsafe bool TryReadHeldDirect(IntPtr list, long ownerId, List<Stamp> into)
    {
        _itemList ??= RawList.Learn(IL2CPP.il2cpp_object_get_class(list));

        if (_itemList == null || !_itemList.TryOpen(list, out var items, out var count)) return GiveUp("an item list");

        for (var i = 0; i < count; i++)
        {
            var item = RawList.Item(items, i);

            if (item == IntPtr.Zero) continue;

            if (*(IntPtr*)item != _itemClass) return GiveUp("an item stack");

            var owner = *(long*)(item + _ownerIdAt);
            var itemCount = *(int*)(item + _countAt);

            if (owner != ownerId || itemCount <= 0) continue;

            into.Add(new Stamp
            {
                Pointer = item, Id = *(long*)(item + _instanceIdAt), OwnerId = ownerId, ConfigId = *(int*)(item + _configIdAt), Count = itemCount,
                UseTimes = *(int*)(item + _useTimesAt), MaxUseTimes = *(int*)(item + _maxUseTimesAt), InstanceWeight = *(int*)(item + _instanceWeightAt),
                Width = *(int*)(item + _sizeAt), Height = *(int*)(item + _sizeAt + 4), X = *(int*)(item + _posAt), Y = *(int*)(item + _posAt + 4),
            });
        }

        return true;
    }

    /// <summary>Finds where each field the station reads sits, from the game's own type data, once.</summary>
    private void Learn()
    {
        if (_hasLearned) return;

        _hasLearned = true;

        try
        {
            _itemClass = Il2CppClassPointerStore<ItemData>.NativeClassPtr;
            _instanceIdAt = RawDictionary.FieldOffset(Il2CppClassPointerStore<BaseEntity>.NativeClassPtr, "<InstanceId>k__BackingField");
            _agentConfigIdAt = RawDictionary.FieldOffset(Il2CppClassPointerStore<BaseAgent>.NativeClassPtr, "<AgentConfigId>k__BackingField");
            _ownerIdAt = RawDictionary.FieldOffset(_itemClass, "<OwnerId>k__BackingField");
            _configIdAt = RawDictionary.FieldOffset(_itemClass, "<ItemConfigId>k__BackingField");
            _countAt = RawDictionary.FieldOffset(_itemClass, "<ItemCount>k__BackingField");
            _sizeAt = RawDictionary.FieldOffset(_itemClass, "<ItemSize>k__BackingField");
            _posAt = RawDictionary.FieldOffset(_itemClass, "<BagPos>k__BackingField");
            _useTimesAt = RawDictionary.FieldOffset(_itemClass, "<UseTimes>k__BackingField");
            _maxUseTimesAt = RawDictionary.FieldOffset(_itemClass, "<MaxUseTimes>k__BackingField");
            _instanceWeightAt = RawDictionary.FieldOffset(_itemClass, "<InstanceWeight>k__BackingField");
            _isDirect = _itemClass != IntPtr.Zero && _instanceIdAt > 0 && _agentConfigIdAt > 0 && _ownerIdAt > 0 && _configIdAt > 0 && _countAt > 0 && _sizeAt > 0
                && _posAt > 0 && _useTimesAt > 0 && _maxUseTimesAt > 0 && _instanceWeightAt > 0;
        }
        catch (Exception ex)
        {
            _isDirect = false;
            _log("reading stacks straight from memory is off, the game's objects are read instead: " + ex.Message);

            return;
        }

        if (!_isDirect) _log("reading stacks straight from memory is off (a field was not found), the game's objects are read instead");
    }

    private bool GiveUp(string what)
    {
        _isDirect = false;
        _log($"reading {what} straight from memory found something unexpected, the game's objects are read from now on");

        return false;
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
