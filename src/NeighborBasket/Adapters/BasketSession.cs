using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using NeighborBasket.Core;
using UnityEngine;

namespace NeighborBasket.Adapters;

/// <summary>Runs the buttons' presses, the warning checks and the gift log, one frame at a time on the game's thread.</summary>
internal sealed class BasketSession
{
    private const float WarningCheckSeconds = 5f;
    private const float GiftPollSeconds = 1f;
    // The game runs a placing move a moment after it is sent. The fill finishes as soon as every placed stack is in the
    // basket, and after this long at most when some never arrive.
    private const float FillWaitSeconds = 0.6f;

    private readonly BasketGame _game;
    private readonly GiftJournal _journal;
    private readonly WebPage _page;
    private readonly Toasts _toasts;
    private readonly Action<string> _log;
    private readonly HashSet<string> _shownWarnings = new();
    private float _nextWarningCheckAt;
    private float _nextGiftPollAt;
    private bool? _lastIsChinese;
    private bool _hasWarningsFailed;
    private bool _hasGiftLogFailed;
    private bool _hasReportedWeight;
    private bool _hasStationFailed;
    private PendingFill? _pendingFill;
    private readonly StationFeed _feed = new();
    // The station's answer is built once at the end of the frame, however many asks and moves came in during it.
    private bool _isStationWanted;
    private bool _isStationFresh;
    private string? _planKey;
    private FillResult? _planCache;
    // Where each stack the station put in the basket came from, by item id and by item kind, so Take puts it back there.
    private readonly Dictionary<long, long> _originById = new();
    private readonly Dictionary<int, long> _originByConfig = new();

    private sealed record PendingFill(long BasketId, FillResult Plan, List<long> Placed, Dictionary<int, string> Names, bool IsChinese, float Deadline);

    public BasketSession(BasketGame game, GiftJournal journal, WebPage page, Toasts toasts, Action<string> log)
    {
        _game = game;
        _journal = journal;
        _page = page;
        _toasts = toasts;
        _log = log;
    }

    public bool ShowFill { get; set; } = true;

    /// <summary>The progress Fill aims for (80 lifts her condition two steps); 0 packs the basket full every time.</summary>
    public int FillGoal { get; set; } = 80;

    /// <summary>The most of any one item kind Fill takes, in percent of what you have at home; 100 is no cap.</summary>
    public int FillShare { get; set; } = 30;

    public bool ShowCallBack { get; set; }

    public bool ShowPutAway { get; set; } = true;

    public bool ShowApp { get; set; } = true;

    public bool AreWarningsOn { get; set; } = true;

    public bool WarnStreak { get; set; } = true;

    public int WarnHours { get; set; } = 12;

    public bool ShowHudAlert { get; set; } = true;

    /// <summary>Sends the buttons' settings to the page again, after a setting changes.</summary>
    public void Resetup() => _lastIsChinese = null;

    public void Update()
    {
        var now = Time.unscaledTime;
        var isChinese = GameLanguage.IsChinese();

        _page.KeepInstalled(now);

        if (_lastIsChinese != isChinese)
        {
            _lastIsChinese = isChinese;
            Setup(isChinese);
        }

        while (PageMessages.TryTake(out var command)) Handle(command, isChinese);

        if (_pendingFill is { } pending && FillWait.IsDone(pending.Placed, _game.BasketItemIds(pending.BasketId), now, pending.Deadline))
        {
            _pendingFill = null;
            RunGuarded("fill check", () => FinishFill(pending), isChinese);
        }

        // While a fill is under way the basket is half moved, so the station waits for the finished one.
        if (_isStationWanted && _pendingFill == null)
        {
            var isFresh = _isStationFresh;

            _isStationWanted = false;
            _isStationFresh = false;
            ShowStation(isFresh, isChinese);
        }

        if (now >= _nextWarningCheckAt)
        {
            _nextWarningCheckAt = now + WarningCheckSeconds;
            CheckWarnings(isChinese);
        }

        if (now >= _nextGiftPollAt)
        {
            _nextGiftPollAt = now + GiftPollSeconds;
            PollGifts();
        }
    }

    // Put away shows only with Storage Hub there to do it.
    private void Setup(bool isChinese) => _page.Setup(ShowFill, ShowCallBack, ShowPutAway && StorageHubLink.IsAvailable, ShowApp, isChinese);

    private void Handle(PageCommand command, bool isChinese)
    {
        switch (command)
        {
            case PageCommand.Fill fill:
                RunGuarded("fill", () => Fill(fill.IsMax, isChinese), isChinese);

                // The page drew the planned stacks as landing; a fill that placed none answers now.
                if (_pendingFill == null) PushStation();

                break;
            case PageCommand.CallBack:
                RunGuarded("call back", () => CallBack(isChinese), isChinese);
                PushStation();
                break;
            case PageCommand.PutAway:
                RunGuarded("put away", () => PutAway(isChinese), isChinese);
                PushStation();
                break;
            case PageCommand.App:
                RunGuarded("phone app", () => AppJson(isChinese), isChinese);
                break;
            case PageCommand.Station station:
                _isStationWanted = true;

                if (station.IsFresh)
                {
                    _isStationFresh = true;
                    _game.ForgetKinds();
                }

                break;
            case PageCommand.Add add:
                RunGuarded("add", () => Add(add, isChinese), isChinese);
                break;
            case PageCommand.Take take:
                RunGuarded("take", () => Take(take, isChinese), isChinese);
                break;
            case PageCommand.TakeAll:
                RunGuarded("to bag", () => TakeAll(isChinese), isChinese);
                break;
            case PageCommand.BreakDown:
                RunGuarded("break down", () => BreakDown(isChinese), isChinese);
                break;
            case PageCommand.Ready:
                _log("page script ready");
                Setup(isChinese);
                break;
            case PageCommand.NoScript:
                _page.Install();
                break;
            case PageCommand.Unknown unknown:
                _log("page says: " + unknown.Text);
                break;
        }
    }

    /// <summary>
    /// Plans the basket from home storage, enough for the goal (or as full as it goes, with isMax), and sends each whole
    /// stack to its planned cell. The game runs those moves a moment later, so the outcome toast and the part stacks
    /// (moved one item at a time, which finds its own cell) wait for FinishFill.
    /// </summary>
    private void Fill(bool isMax, bool isChinese)
    {
        if (_pendingFill != null) return;

        var screen = _game.OpenBasket();

        if (screen == null)
        {
            _toasts.Show(Texts.NotOpen.Pick(isChinese), true, isChinese);

            return;
        }

        var stock = ReadStock(screen.BasketId);

        if (stock == null)
        {
            _toasts.Show(Texts.Failed.Pick(isChinese), true, isChinese);

            return;
        }

        var plan = isMax ? MaxPlan(screen, stock) : Plan(screen, stock);

        foreach (var pick in plan.Picks.Where(pick => !pick.IsPart)) BasketGame.PlaceAt(pick, screen.BasketId);

        _log($"fill{(isMax ? " max" : $" to {FillGoal}, stage needs {screen.Wish.StageLeft}")} share {FillShare}%: wish [{string.Join(",", screen.Wish.Categories)}] crisis {screen.Wish.CrisisCategory?.ToString() ?? "-"} dying {screen.Wish.IsDying}, " +
             $"{stock.Station.Sources.Count} stacks at hand, basket {stock.Station.Cols}x{stock.Station.Rows} holding {stock.Station.InBasket.Count}, weight {stock.Station.UsedWeight}/{stock.Station.MaxWeight}, " +
             $"picked {string.Join(",", plan.Picks.Select(pick => $"{pick.Item.ConfigId}x{pick.Item.Count}{(pick.IsPart ? "p" : "")}@{pick.X},{pick.Y}w{pick.Weight}v{pick.Item.Value}"))}, " +
             $"missing [{string.Join(",", plan.Missing)}], gain {plan.Gain}, stop {plan.Stop}");

        if (plan.Picks.Count == 0)
        {
            _toasts.Show(Texts.Outcome(plan, 0, Names(screen, isChinese), isChinese), true, isChinese);

            return;
        }

        foreach (var pick in plan.Picks) Remember(pick.Item.ItemId, pick.Item.ConfigId, pick.Item.OwnerId);

        var placed = plan.Picks.Where(pick => !pick.IsPart).Select(pick => pick.Item.ItemId).ToList();

        _pendingFill = new PendingFill(screen.BasketId, plan, placed, Names(screen, isChinese), isChinese, Time.unscaledTime + FillWaitSeconds);
    }

    /// <summary>
    /// Moves the part stacks, one item at a time, once the whole ones are in; then counts the planned stacks that
    /// reached the basket and says how the fill went. A part that moved short counts as not reached.
    /// </summary>
    private void FinishFill(PendingFill pending)
    {
        var held = _game.BasketItemIds(pending.BasketId);
        var failed = pending.Plan.Picks.Count(pick => !pick.IsPart && !held.Contains(pick.Item.ItemId));

        foreach (var pick in pending.Plan.Picks.Where(pick => pick.IsPart))
        {
            var moved = _game.MoveUnits(pick.Item.ItemId, pick.Item.OwnerId, pending.BasketId, pick.Item.Count);

            if (moved < pick.Item.Count)
            {
                failed++;
                _log($"fill: {moved} of {pick.Item.Count} of {pick.Item.ConfigId} reached the basket");
            }
        }

        var text = Texts.Outcome(pending.Plan, failed, pending.Names, pending.IsChinese);

        if (failed > 0) _log($"fill: {failed} of {pending.Plan.Picks.Count} planned stacks did not reach the basket");

        if (StruggleNeed() is { } need && _game.BasketSatiety(pending.BasketId) is { } satiety) text += " " + Texts.BasketSatiety.Say(pending.IsChinese, (int)Math.Floor(satiety), need);

        _toasts.Show(text, failed > 0, pending.IsChinese);
        PushStation();
    }

    /// <summary>One item of a stack, or all of it, from wherever it sits into the basket.</summary>
    private void Add(PageCommand.Add add, bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null || screen.Wish.IsAway)
        {
            _toasts.Show((screen == null ? Texts.NotOpen : Texts.Away).Pick(isChinese), true, isChinese);

            return;
        }

        if (_game.Find(add.ItemId) is not { } item || item.OwnerId == screen.BasketId) return;

        if (GameCore.HotUpdate.NeighborRules.IsBasketForbidItem(item.ConfigId))
        {
            _toasts.Show(Texts.CannotTake.Pick(isChinese), true, isChinese);

            return;
        }

        var isMoved = _game.Move(add.ItemId, item.OwnerId, screen.BasketId, add.IsWholeStack);

        _log($"add {item.ConfigId} ({(add.IsWholeStack ? "stack" : "one")}) from {item.OwnerId}: {(isMoved ? "moved" : "refused")}");

        if (isMoved) Remember(add.ItemId, item.ConfigId, item.OwnerId);
        else _toasts.Show(Texts.AddFailed.Say(isChinese, item.Name), true, isChinese);

        PushStation();
    }

    /// <summary>A stack from the basket back where the station took it from, or to the bag when that is unknown or full.</summary>
    private void Take(PageCommand.Take take, bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null || _game.Find(take.ItemId) is not { } item || item.OwnerId != screen.BasketId) return;

        var bagId = _game.BagId();
        var origin = _originById.TryGetValue(take.ItemId, out var byId) ? byId : _originByConfig.TryGetValue(item.ConfigId, out var byConfig) ? byConfig : bagId;
        var isMoved = origin != 0 && _game.Move(take.ItemId, screen.BasketId, origin, true);

        if (!isMoved && bagId != 0 && origin != bagId) isMoved = _game.Move(take.ItemId, screen.BasketId, bagId, true);

        _log($"take {item.ConfigId} back to {origin}: {(isMoved ? "moved" : "refused")}");

        if (isMoved) _originById.Remove(take.ItemId);
        else _toasts.Show(Texts.TakeFailed.Say(isChinese, item.Name), true, isChinese);

        PushStation();
    }

    /// <summary>Everything in the basket, her gifts included, into the bag; what does not fit stays and is reported.</summary>
    private void TakeAll(bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null)
        {
            _toasts.Show(Texts.NotOpen.Pick(isChinese), true, isChinese);

            return;
        }

        var station = ReadStock(screen.BasketId)?.Station;

        if (station == null || station.BagId == 0 || station.InBasket.Count == 0) return;

        var failed = 0;

        foreach (var item in station.InBasket)
        {
            if (_game.Move(item.Id, screen.BasketId, station.BagId, true)) _originById.Remove(item.Id);
            else failed++;
        }

        _log($"to bag: {station.InBasket.Count - failed} of {station.InBasket.Count} stacks moved");

        if (failed > 0) _toasts.Show(Texts.TakeAllShort.Say(isChinese, failed, station.InBasket.Count), true, isChinese);

        PushStation();
    }

    /// <summary>
    /// Breaks down her gifts in the basket that the game lets you take apart by hand. Each goes to the bag first, where
    /// the game's own Use works on it, then its use is lined up; the character does them one after another, taking the
    /// game's own time. Gifts that do not fit in the bag, or that the game's use check refuses, stay and are reported.
    /// </summary>
    private void BreakDown(bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null)
        {
            _toasts.Show(Texts.NotOpen.Pick(isChinese), true, isChinese);

            return;
        }

        var station = ReadStock(screen.BasketId)?.Station;
        var gifts = station?.InBasket.Where(item => item.Breaks).ToList();

        if (station == null || station.BagId == 0 || gifts == null || gifts.Count == 0) return;

        var lined = 0;

        _game.ClearLine();

        foreach (var gift in gifts)
        {
            if (!_game.Move(gift.Id, screen.BasketId, station.BagId, true)) continue;

            _originById.Remove(gift.Id);

            if (_game.LineUpBreakdown(gift.Id)) lined++;
        }

        var started = lined > 0 && _game.StartLine();
        var done = started ? lined : 0;

        _log($"break down: {done} of {gifts.Count} gift(s) lined up ({string.Join(", ", gifts.Select(gift => gift.Name))})");

        if (done > 0) _toasts.Show(Texts.BreakStarted.Say(isChinese, done), false, isChinese);
        if (done < gifts.Count) _toasts.Show(Texts.BreakShort.Say(isChinese, gifts.Count - done, gifts.Count), true, isChinese);

        PushStation();
    }

    /// <summary>
    /// The page already drew the move and waits for this answer to confirm it, or to put back a refused one, so it goes
    /// out at the end of this frame even when it matches the last one.
    /// </summary>
    private void PushStation()
    {
        _isStationWanted = true;
        _feed.Forget();
    }

    private void Remember(long itemId, int configId, long ownerId)
    {
        _originById[itemId] = ownerId;
        _originByConfig[configId] = ownerId;
    }

    private BasketStock? ReadStock(long basketId)
    {
        var stock = _game.ReadStock(basketId);

        if (stock?.WeightNote != null && !_hasReportedWeight)
        {
            _hasReportedWeight = true;
            _log("reading the basket's weight failed, fill ignores weight and the game refuses what does not fit (reported once): " + stock.WeightNote);
        }

        return stock;
    }

    /// <summary>
    /// The goal plan from home storage only, so the bag is never emptied without a click. The station asks about once
    /// a second, so a plan is kept until what is at hand, the basket, her wish or the goal changes.
    /// </summary>
    private FillResult Plan(BasketScreen screen, BasketStock stock)
    {
        var station = stock.Station;
        var sources = HomeSources(station);
        var canSplit = StackingUnits.IsAvailable;
        var key = string.Join(";", FillGoal, FillShare, canSplit, screen.Wish.StageLeft,
            screen.Wish.IsActive, screen.Wish.IsAway, screen.Wish.IsDying, screen.Wish.IsPending, screen.Wish.CrisisCategory, string.Join(",", screen.Wish.Categories), station.MaxWeight, station.UsedWeight,
            string.Join(",", station.InBasket.Select(item => $"{item.Id}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Category}")),
            string.Join(",", sources.Select(item => $"{item.Id}:{item.OwnerId}:{item.Count}:{item.Value}:{item.Weight}")));

        if (key == _planKey && _planCache != null) return _planCache;

        var scoreNow = FillPlan.Score(station.InBasket.Select(item => item.ToCandidate()), screen.Wish, new int[0], Rates.Game);
        var plan = FillPlan.Goal(screen.Wish, sources.Select(item => item.ToCandidate()).ToList(), station.ToBasket(screen.Wish, stock.CategoriesInBasket), Rates.Game, FillGoal, scoreNow, canSplit, Limits(station));

        _planKey = key;
        _planCache = plan;

        return plan;
    }

    /// <summary>The most the basket holds, whole stacks only: Shift+Fill. The per-item cap still holds.</summary>
    private FillResult MaxPlan(BasketScreen screen, BasketStock stock) =>
        FillPlan.Plan(screen.Wish, HomeSources(stock.Station).Select(item => item.ToCandidate()).ToList(), stock.Station.ToBasket(screen.Wish, stock.CategoriesInBasket), Rates.Game, Limits(stock.Station));

    private IReadOnlyDictionary<int, int>? Limits(StationData station) =>
        FillPlan.Limits(HomeSources(station).Select(item => item.ToCandidate()), station.InBasket.Select(item => item.ToCandidate()), FillShare);

    private static List<StationItem> HomeSources(StationData station) => station.Sources.Where(item => item.OwnerId != station.BagId).ToList();

    private static Dictionary<int, string> Names(BasketScreen screen, bool isChinese)
    {
        var names = new Dictionary<int, string>(screen.Names);

        if (!names.ContainsKey(FillPlan.FoodCategory)) names[FillPlan.FoodCategory] = Texts.Food.Pick(isChinese);

        return names;
    }

    /// <summary>
    /// Answers the station's once-a-second ask with everything it draws, only when something changed or the page holds
    /// nothing yet. A failure turns the station off quietly (it asks every second, so a toast each time would bury the
    /// screen) and is logged once; the game's own screen stays.
    /// </summary>
    private void ShowStation(bool isFresh, bool isChinese)
    {
        if (_hasStationFailed)
        {
            if (isFresh) _page.Station("false");

            return;
        }

        try
        {
            if (_feed.Next(StationJson(isChinese), isFresh) is { } json) _page.Station(json);
        }
        catch (Exception ex)
        {
            _hasStationFailed = true;
            _log("the basket station failed and is off until the next start: " + ex);
            _page.Station("false");
        }
    }

    // The phone has no basket screen open, so her trouble shows under its general name.
    private void AppJson(bool isChinese)
    {
        var line = ReadLine();

        _page.App(AppView.Json(line?.Need, line?.Tracker, null, WarnHours, _journal.Entries, isChinese));
    }

    // Null while the basket screen is not open: the page may still be closing, and nothing is the right answer then.
    private string? StationJson(bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null) return null;

        var stock = ReadStock(screen.BasketId);

        if (stock == null) return null;

        var line = ReadLine();
        string? troubleName = null;

        if (screen.Wish.CrisisCategory is { } crisis && screen.Names.TryGetValue(crisis, out var name)) troubleName = name;

        var plan = ShowFill && !screen.Wish.IsAway ? Plan(screen, stock) : null;

        return StationView.Json(stock.Station, screen.Wish, line?.Need, line?.Tracker, troubleName, plan, WarnHours, isChinese);
    }

    /// <summary>The least satiety that pulls her through, while she struggles; null otherwise or when it cannot be read.</summary>
    private int? StruggleNeed()
    {
        try
        {
            return ReadLine()?.Need is { StruggleState: 1 } need ? need.SatietyMin : null;
        }
        catch (Exception ex)
        {
            _log("reading the satiety she needs failed: " + ex.Message);

            return null;
        }
    }

    private void PutAway(bool isChinese)
    {
        if (!StorageHubLink.IsAvailable)
        {
            _toasts.Show(Texts.NoStorageHub.Pick(isChinese), true, isChinese);

            return;
        }

        if (_game.OpenBasket() == null)
        {
            _toasts.Show(Texts.NotOpen.Pick(isChinese), true, isChinese);

            return;
        }

        var said = StorageHubLink.PutAwayBasket();

        _log("put away: " + (said ?? "Storage Hub is not ready"));

        if (!string.IsNullOrEmpty(said)) _toasts.Show(said!, false, isChinese);
    }

    private LineRead? ReadLine() => GameSingleton.Existing<BattleLogicWorld>() == null ? null : NeedReader.Read();

    private void PollGifts()
    {
        if (_hasGiftLogFailed) return;

        try
        {
            if (GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager is not { } time || !_journal.FollowSave()) return;

            var line = NeedReader.Read();

            _journal.See(new BasketLook(_game.IsBasketAway(), _game.BasketContents(_game.HomeBasketId()), line?.Progress ?? 0, line?.Affinity ?? 0, time.GetDay(), time.GetHour24()));
        }
        catch (Exception ex)
        {
            // A renamed field would fail here every second; one line is enough.
            _hasGiftLogFailed = true;
            _log("the gift log failed and is off until the next start: " + ex);
        }
    }

    private void CallBack(bool isChinese)
    {
        if (!ShowCallBack) return;

        var isCalled = _game.CallBack();

        _log(isCalled ? "basket called back" : "call back: the basket is not away");
        _toasts.Show((isCalled ? Texts.CalledBack : Texts.NotAway).Pick(isChinese), !isCalled, isChinese);
    }

    private void CheckWarnings(bool isChinese)
    {
        if (GameSingleton.Existing<BattleLogicWorld>() == null)
        {
            _shownWarnings.Clear();
            ShowHud("null");

            return;
        }

        if (_hasWarningsFailed || (!AreWarningsOn && !ShowHudAlert))
        {
            ShowHud("null");

            return;
        }

        NeedState? read;

        try
        {
            read = NeedReader.Read()?.Need;
        }
        catch (Exception ex)
        {
            // A game update that renames the clock's fields would fail here every few seconds; one line is enough.
            _hasWarningsFailed = true;
            _log("reading the neighbor's state failed, warnings are off until the next start: " + ex);
            ShowHud("null");

            return;
        }

        if (read is not { } state)
        {
            ShowHud("null");

            return;
        }

        if (!WarnStreak) state = state with { StreakDays = 0 };

        ShowHud(ShowHudAlert ? HudView.Json(Warnings.Active(state, WarnHours), isChinese) : "null");

        if (!AreWarningsOn) return;

        foreach (var warning in Warnings.Check(state, WarnHours, _shownWarnings))
        {
            _log($"warning {warning.Kind}, {warning.HoursLeft} h left ({warning.Key}); tier {state.Tier}, floor {state.TierFloor}, struggle {state.StruggleState}");
            _toasts.Show(Texts.Warn(warning, isChinese), true, isChinese);
        }
    }

    // Sent on every check, not only on a change, so a page that reloaded gets the line back within one check.
    private void ShowHud(string json) => _page.Hud(json);

    private void RunGuarded(string what, Action action, bool isChinese)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log($"{what} failed: {ex}");
            _toasts.Show(Texts.Failed.Pick(isChinese), true, isChinese);
        }
    }
}
