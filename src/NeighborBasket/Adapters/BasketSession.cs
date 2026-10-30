using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// Runs the buttons' presses, the warning checks and the gift log, one frame at a time on the game's thread. Nothing
/// it sends to the page is built again while what it is drawn from holds still: the station, the phone app and the
/// alert line each go out only when they changed, or when the page holds nothing.
/// </summary>
internal sealed class BasketSession
{
    private const float WarningCheckSeconds = 5f;
    private const float GiftPollSeconds = 1f;
    private const float LanguageCheckSeconds = 1f;
    // The game runs a placing move a moment after it is sent. The fill finishes as soon as every placed stack is in the
    // basket, and after this long at most when some never arrive.
    private const float FillWaitSeconds = 0.6f;

    // Read instead of Unity's clock, which crosses into the game for every read.
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly BasketGame _game;
    private readonly GiftJournal _journal;
    private readonly WebPage _page;
    private readonly Toasts _toasts;
    private readonly Action<string> _log;
    private readonly WebKeyboard _keyboard = new();
    private readonly HashSet<string> _shownWarnings = new();
    // The timers start apart, so the gift poll and the warning check never land on the same frame.
    private float _nextWarningCheckAt = 0.5f;
    private float _nextGiftPollAt = 0.25f;
    private float _nextLanguageCheckAt;
    private bool _isChinese;
    private string? _languageKey;
    private bool? _lastIsChinese;
    private bool _hasWarningsFailed;
    private bool _hasGiftLogFailed;
    private bool _hasReportedWeight;
    private bool _hasStationFailed;
    private IntPtr _primedWorld;
    private PendingFill? _pendingFill;
    private readonly StationFeed _feed = new();
    // The station's answer is built once at the end of the frame, however many asks and moves came in during it.
    private bool _isStationWanted;
    private bool _isStationFresh;
    private ulong? _planKey;
    private FillResult? _planCache;
    // What the page was last sent for the alert line and the phone app, so an unchanged one is not sent again.
    private string? _lastHud;
    private bool _hasBasketFailed;
    private BasketState? _basket;
    private string _basketContents = "";
    private ulong? _lastAppPrint;
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

    public bool ShowBasketOnHud { get; set; } = true;

    /// <summary>Sends the buttons' settings and the HUD lines to the page again, after a setting changes.</summary>
    public void Resetup()
    {
        _lastIsChinese = null;
        _nextWarningCheckAt = 0;
    }

    private static float Now => (float)Clock.Elapsed.TotalSeconds;

    public void Update()
    {
        var now = Now;

        _page.KeepInstalled(now);

        if (now >= _nextLanguageCheckAt)
        {
            _nextLanguageCheckAt = now + LanguageCheckSeconds;
            FollowLanguage();
            PrimeStation();
        }

        var isChinese = _isChinese;

        if (_lastIsChinese != isChinese)
        {
            _lastIsChinese = isChinese;
            Setup(isChinese);
        }

        while (PageMessages.TryTake(out var command)) Handle(command, isChinese);

        if (_pendingFill != null && FillWait.IsDone(_pendingFill.Placed, _game.BasketItemIds(_pendingFill.BasketId), now, _pendingFill.Deadline))
        {
            var pending = _pendingFill;

            _pendingFill = null;
            FinishFillGuarded(pending);
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
            PollBasket();
            PollGifts();
        }
    }

    /// <summary>
    /// Reads the game's language once a second. A switch drops every name read in the old one and everything sent to
    /// the page in it, so all of it goes out again in the new one.
    /// </summary>
    private void FollowLanguage()
    {
        var key = GameLanguage.Key();

        _isChinese = GameLanguage.IsChinese();
        _game.SetLanguage(key);

        if (_languageKey == null || key == _languageKey)
        {
            _languageKey = key;

            return;
        }

        _languageKey = key;
        _feed.Forget();
        _lastAppPrint = null;
        _lastHud = null;
        _planKey = null;
        _nextWarningCheckAt = 0;
    }

    /// <summary>
    /// The first basket station of a game would read every stack at home through the game for the first time inside the
    /// frame its screen opens on. Once the player is in a newly loaded game, that read is done once here instead, while
    /// the game is still coming in, so the station later only reads what changed since. Read-only; a failure only costs
    /// the head start.
    /// </summary>
    private void PrimeStation()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();

        if (world == null || world.Pointer == _primedWorld || world._AgentManager?.GetLeadingRole() == null) return;

        _primedWorld = world.Pointer;

        try
        {
            var basketId = _game.HomeBasketId();

            if (basketId != 0) _game.ReadStock(basketId);
        }
        catch (Exception ex)
        {
            _log("reading home storage ahead of the first basket station failed, it is read when the station opens: " + ex.Message);
        }
    }

    // Put away shows only with Storage Hub there to do it.
    private void Setup(bool isChinese) => _page.Setup(ShowFill, ShowCallBack, ShowPutAway && StorageHubLink.IsAvailable, ShowApp, isChinese);

    private void Handle(PageCommand command, bool isChinese)
    {
        switch (command)
        {
            case PageCommand.Fill:
                Guarded(command, isChinese);

                // The page drew the planned stacks as landing; a fill that placed none answers now.
                if (_pendingFill == null) PushStation();

                break;
            case PageCommand.CallBack:
            case PageCommand.PutAway:
                Guarded(command, isChinese);
                PushStation();
                break;
            case PageCommand.App:
            case PageCommand.Add:
            case PageCommand.Take:
            case PageCommand.TakeAll:
            case PageCommand.BreakDown:
                Guarded(command, isChinese);
                break;
            case PageCommand.Station station:
                _isStationWanted = true;

                if (station.IsFresh) _isStationFresh = true;

                break;
            case PageCommand.Ready:
                // A page that just put the script in holds nothing: the buttons, the alert line and the phone app go again.
                _log("page script ready");
                Setup(isChinese);
                _lastAppPrint = null;

                if (_lastHud != null) _page.Hud(_lastHud);

                break;
            case PageCommand.NoScript:
                // A page that lost its script lost its search box too.
                _keyboard.Release();
                _page.Install();
                break;
            case PageCommand.Focus { IsOn: true }:
                _keyboard.Claim();
                break;
            case PageCommand.Focus:
                _keyboard.Release();
                break;
            case PageCommand.Unknown unknown:
                _log("page says: " + unknown.Text);
                break;
        }
    }

    /// <summary>Runs one button's work; a failure is logged and shown, never thrown into the frame.</summary>
    private void Guarded(PageCommand command, bool isChinese)
    {
        try
        {
            switch (command)
            {
                case PageCommand.Fill fill:
                    Fill(fill.IsMax, isChinese);
                    break;
                case PageCommand.CallBack:
                    CallBack(isChinese);
                    break;
                case PageCommand.PutAway:
                    PutAway(isChinese);
                    break;
                case PageCommand.App app:
                    SendApp(app.IsFresh, isChinese);
                    break;
                case PageCommand.Add add:
                    Add(add, isChinese);
                    break;
                case PageCommand.Take take:
                    Take(take, isChinese);
                    break;
                case PageCommand.TakeAll:
                    TakeAll(isChinese);
                    break;
                case PageCommand.BreakDown:
                    BreakDown(isChinese);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log($"{What(command)} failed: {ex}");
            _toasts.Show(Texts.Failed.Pick(isChinese), true, isChinese);
        }
    }

    private static string What(PageCommand command) => command switch
    {
        PageCommand.Fill => "fill",
        PageCommand.CallBack => "call back",
        PageCommand.PutAway => "put away",
        PageCommand.App => "phone app",
        PageCommand.Add => "add",
        PageCommand.Take => "take",
        PageCommand.TakeAll => "to bag",
        PageCommand.BreakDown => "break down",
        _ => "page command",
    };

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

        _pendingFill = new PendingFill(screen.BasketId, plan, placed, Names(screen, isChinese), isChinese, Now + FillWaitSeconds);
    }

    private void FinishFillGuarded(PendingFill pending)
    {
        try
        {
            FinishFill(pending);
        }
        catch (Exception ex)
        {
            _log($"fill check failed: {ex}");
            _toasts.Show(Texts.Failed.Pick(pending.IsChinese), true, pending.IsChinese);
        }
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
        var canSplit = StackingUnits.IsAvailable;
        var key = Print.Mix(Print.Mix(Print.Mix(Print.Mix(stock.Print, Print.Wish(screen.Wish, null)), FillGoal), FillShare), canSplit);

        if (key == _planKey && _planCache != null) return _planCache;

        var sources = HomeSources(station);
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
    /// nothing yet. What it is drawn from is fingerprinted first, so an unchanged answer is neither planned nor built.
    /// A failure turns the station off quietly (it asks every second, so a toast each time would bury the screen) and
    /// is logged once; the game's own screen stays.
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
            // Nothing while the basket screen is not open: the page may still be closing, and nothing is the right answer then.
            var screen = _game.OpenBasket();

            if (screen == null) return;

            var stock = ReadStock(screen.BasketId);

            if (stock == null) return;

            var line = ReadLine();
            var print = StationPrint(screen, stock, line, isChinese);

            if (_feed.IsUnchanged(print, isFresh, out var resend))
            {
                if (resend != null) _page.Station(resend);

                return;
            }

            if (_feed.Next(StationJson(screen, stock, line, isChinese), isFresh, print) is { } json) _page.Station(json);
        }
        catch (Exception ex)
        {
            _hasStationFailed = true;
            _log("the basket station failed and is off until the next start: " + ex);
            _page.Station("false");
        }
    }

    /// <summary>Everything the station's answer is built from: the stock, her wish and status, and the settings it shows.</summary>
    private ulong StationPrint(BasketScreen screen, BasketStock stock, LineRead? line, bool isChinese)
    {
        var print = Print.Mix(stock.Print, Print.Wish(screen.Wish, screen.Names));

        print = Print.Mix(print, Print.Line(line?.Need, line?.Tracker));
        print = Print.Mix(print, WarnHours);
        print = Print.Mix(print, ShowFill);
        print = Print.Mix(print, FillGoal);
        print = Print.Mix(print, FillShare);
        print = Print.Mix(print, StackingUnits.IsAvailable);

        return Print.Mix(print, isChinese);
    }

    /// <summary>
    /// The phone app's data, sent only when the log, her status or the language changed since the last one, or when the
    /// page holds none (fresh). The phone has no basket screen open, so her trouble shows under its general name.
    /// </summary>
    private void SendApp(bool isFresh, bool isChinese)
    {
        var line = ReadLine();
        var print = Print.Mix(Print.Mix(Print.Mix(Print.Mix(Print.Start, _journal.Version), Print.Line(line?.Need, line?.Tracker)), WarnHours), isChinese);

        print = BasketView.Mix(print, _basket);

        if (!isFresh && print == _lastAppPrint) return;

        _lastAppPrint = print;
        _page.App(AppView.Json(line?.Need, line?.Tracker, null, WarnHours, _journal.Entries, isChinese, _basket));
    }

    private string StationJson(BasketScreen screen, BasketStock stock, LineRead? line, bool isChinese)
    {
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

    /// <summary>
    /// Reads the basket at home once a second, for the gift log, the phone app and the HUD. A new state (away, home, or
    /// other items) brings the HUD check forward, so its line follows the basket within a second. No basket furniture
    /// at home means no basket to show.
    /// </summary>
    private void PollBasket()
    {
        if (_hasBasketFailed) return;

        try
        {
            if (GameSingleton.Existing<BattleLogicWorld>() == null)
            {
                SetBasket(null);

                return;
            }

            var basketId = _game.HomeBasketId();
            var isAway = _game.IsBasketAway();

            _basketContents = basketId == 0 ? "" : _game.BasketContents(basketId);

            if (basketId == 0 && !isAway)
            {
                SetBasket(null);

                return;
            }

            var items = basketId == 0 ? Array.Empty<string>() : _game.BasketItems;

            if (_basket == null || _basket.IsAway != isAway || !ReferenceEquals(_basket.Items, items)) SetBasket(new BasketState(isAway, items));
        }
        catch (Exception ex)
        {
            // A renamed field would fail here every second; one line is enough. The gift log reads the same fields.
            _hasBasketFailed = true;
            _log("reading the basket failed, its status and the gift log are off until the next start: " + ex);
            SetBasket(null);
        }
    }

    private void SetBasket(BasketState? basket)
    {
        if (basket == null && _basket == null) return;

        _basket = basket;
        _nextWarningCheckAt = 0;
    }

    /// <summary>The basket on the HUD, or null when that line is off.</summary>
    private BasketState? HudBasket => ShowBasketOnHud ? _basket : null;

    private void PollGifts()
    {
        if (_hasGiftLogFailed || _hasBasketFailed) return;

        try
        {
            if (GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager is not { } time || !_journal.FollowSave()) return;

            var line = NeedReader.Read();

            _journal.See(new BasketLook(_basket?.IsAway ?? false, _basketContents, line?.Progress ?? 0, line?.Affinity ?? 0, time.GetDay(), time.GetHour24()));
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
            ShowHud(HudView.Json(Array.Empty<Warning>(), isChinese, HudBasket));

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
            ShowHud(HudView.Json(Array.Empty<Warning>(), isChinese, HudBasket));

            return;
        }

        if (read is not { } state)
        {
            ShowHud(HudView.Json(Array.Empty<Warning>(), isChinese, HudBasket));

            return;
        }

        if (!WarnStreak) state = state with { StreakDays = 0 };

        ShowHud(HudView.Json(ShowHudAlert ? Warnings.Active(state, WarnHours) : Array.Empty<Warning>(), isChinese, HudBasket));

        if (!AreWarningsOn) return;

        foreach (var warning in Warnings.Check(state, WarnHours, _shownWarnings))
        {
            _log($"warning {warning.Kind}, {warning.HoursLeft} h left ({warning.Key}); tier {state.Tier}, floor {state.TierFloor}, struggle {state.StruggleState}");
            _toasts.Show(Texts.Warn(warning, isChinese), true, isChinese);
        }
    }

    /// <summary>
    /// The alert line goes out only when it changed. A page that reloaded says so (Ready) and gets the last line again
    /// then, so it is not resent on every check.
    /// </summary>
    private void ShowHud(string json)
    {
        if (json == _lastHud) return;

        _lastHud = json;
        _page.Hud(json);
    }
}
