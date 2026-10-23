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
    private bool _hasCardFailed;

    public BasketSession(BasketGame game, GiftJournal journal, WebPage page, Toasts toasts, Action<string> log)
    {
        _game = game;
        _journal = journal;
        _page = page;
        _toasts = toasts;
        _log = log;
    }

    public bool ShowFill { get; set; } = true;

    public bool ShowCallBack { get; set; }

    public bool ShowPutAway { get; set; } = true;

    public bool ShowApp { get; set; } = true;

    public int FillExtra { get; set; } = 5;

    public bool AreWarningsOn { get; set; } = true;

    public bool WarnStreak { get; set; } = true;

    public int WarnHours { get; set; } = 12;

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
            case PageCommand.Fill:
                RunGuarded("fill", () => Fill(isChinese), isChinese);
                break;
            case PageCommand.CallBack:
                RunGuarded("call back", () => CallBack(isChinese), isChinese);
                break;
            case PageCommand.PutAway:
                RunGuarded("put away", () => PutAway(isChinese), isChinese);
                break;
            case PageCommand.App:
                RunGuarded("phone app", () => _page.App(AppView.Json(ReadLine()?.Tracker, _journal.Entries, isChinese)), isChinese);
                break;
            case PageCommand.Card:
                ShowCard(isChinese);
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

    private void Fill(bool isChinese)
    {
        var screen = _game.OpenBasket();

        if (screen == null)
        {
            _toasts.Show(Texts.NotOpen.Pick(isChinese), true, isChinese);

            return;
        }

        var stock = _game.ReadStock(screen.BasketId);

        if (stock == null)
        {
            _toasts.Show(Texts.Failed.Pick(isChinese), true, isChinese);

            return;
        }

        if (stock.WeightNote != null && !_hasReportedWeight)
        {
            _hasReportedWeight = true;
            _log("reading the basket's weight failed, fill ignores weight and the game refuses what does not fit (reported once): " + stock.WeightNote);
        }

        var room = new Room(stock.FreeCells, stock.FreeWeight, FillExtra);
        var plan = FillPlan.Plan(screen.Wish, stock.Sources, stock.CategoriesInBasket, room, StackingUnits.IsAvailable);
        var failed = plan.Picks.Count(pick => !_game.Move(pick, screen.BasketId));

        _log($"fill: wish [{string.Join(",", screen.Wish.Categories)}] crisis {screen.Wish.CrisisCategory?.ToString() ?? "-"} dying {screen.Wish.IsDying}, " +
             $"{stock.Sources.Count} stacks at hand, {stock.FreeCells} cells and {stock.FreeWeight} weight free, extras {FillExtra}, " +
             $"picked {string.Join(",", plan.Picks.Select(pick => $"{pick.Item.ConfigId}x{(pick.IsOneUnit ? 1 : pick.Item.Count)}w{pick.Weight}"))}, " +
             $"missing [{string.Join(",", plan.Missing)}], too heavy {plan.TooHeavy}, stop {plan.Stop}, failed {failed}");

        var names = new Dictionary<int, string>(screen.Names);

        if (!names.ContainsKey(FillPlan.FoodCategory)) names[FillPlan.FoodCategory] = Texts.Food.Pick(isChinese);

        var text = Texts.Outcome(plan, failed, names, isChinese);

        if (StruggleNeed() is { } need && _game.BasketSatiety(screen.BasketId) is { } satiety) text += " " + Texts.BasketSatiety.Say(isChinese, (int)Math.Floor(satiety), need);

        _toasts.Show(text, failed > 0 || plan.Picks.Count == 0, isChinese);
    }

    /// <summary>
    /// Answers the card's once-a-second ask. Reads her line and, with the basket home and Fill on, plans a fill without
    /// moving anything, so the card says what a press would add. A failure turns the card off quietly (it asks every
    /// second, so a toast each time would bury the screen) and is logged once.
    /// </summary>
    private void ShowCard(bool isChinese)
    {
        if (_hasCardFailed) return;

        try
        {
            _page.Card(CardJson(isChinese));
        }
        catch (Exception ex)
        {
            _hasCardFailed = true;
            _log("the basket card failed and is off until the next start: " + ex);
            _page.Card("null");
        }
    }

    private string CardJson(bool isChinese)
    {
        var line = ReadLine();
        var screen = _game.OpenBasket();
        FillResult? plan = null;
        var freeWeight = 0f;
        string? troubleName = null;

        if (screen != null)
        {
            if (screen.Wish.CrisisCategory is { } crisis && screen.Names.TryGetValue(crisis, out var name)) troubleName = name;

            if (ShowFill && !screen.Wish.IsAway && _game.ReadStock(screen.BasketId) is { } stock)
            {
                plan = FillPlan.Plan(screen.Wish, stock.Sources, stock.CategoriesInBasket, new Room(stock.FreeCells, stock.FreeWeight, FillExtra), StackingUnits.IsAvailable);
                freeWeight = stock.FreeWeight;
            }
        }

        return CardView.Json(line?.Need, line?.Tracker, troubleName, plan, freeWeight, WarnHours, isChinese);
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

            return;
        }

        if (!AreWarningsOn || _hasWarningsFailed) return;

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

            return;
        }

        if (read is not { } state) return;

        if (!WarnStreak) state = state with { StreakDays = 0 };

        foreach (var warning in Warnings.Check(state, WarnHours, _shownWarnings))
        {
            _log($"warning {warning.Kind}, {warning.HoursLeft} h left ({warning.Key}); tier {state.Tier}, floor {state.TierFloor}, struggle {state.StruggleState}");
            _toasts.Show(Texts.Warn(warning, isChinese), true, isChinese);
        }
    }

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
