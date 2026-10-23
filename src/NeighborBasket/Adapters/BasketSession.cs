using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using NeighborBasket.Core;
using UnityEngine;

namespace NeighborBasket.Adapters;

/// <summary>Runs the buttons' presses and the warning checks, one frame at a time on the game's thread.</summary>
internal sealed class BasketSession
{
    private const float WarningCheckSeconds = 5f;

    private readonly BasketGame _game;
    private readonly WebPage _page;
    private readonly Toasts _toasts;
    private readonly Action<string> _log;
    private readonly HashSet<string> _shownWarnings = new();
    private float _nextWarningCheckAt;
    private bool? _lastIsChinese;
    private bool _hasWarningsFailed;

    public BasketSession(BasketGame game, WebPage page, Toasts toasts, Action<string> log)
    {
        _game = game;
        _page = page;
        _toasts = toasts;
        _log = log;
    }

    public bool ShowFill { get; set; } = true;

    public bool ShowCallBack { get; set; }

    public bool AreWarningsOn { get; set; } = true;

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
            _page.Setup(ShowFill, ShowCallBack, isChinese);
        }

        while (PageMessages.TryTake(out var command)) Handle(command, isChinese);

        if (now >= _nextWarningCheckAt)
        {
            _nextWarningCheckAt = now + WarningCheckSeconds;
            CheckWarnings(isChinese);
        }
    }

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
            case PageCommand.Ready:
                _log("page script ready");
                _page.Setup(ShowFill, ShowCallBack, isChinese);
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

        var plan = FillPlan.Plan(screen.Wish, stock.Sources, stock.CategoriesInBasket, stock.FreeCells, StackingUnits.IsAvailable);
        var failed = plan.Picks.Count(pick => !_game.Move(pick, screen.BasketId));

        _log($"fill: wish [{string.Join(",", screen.Wish.Categories)}] crisis {screen.Wish.CrisisCategory?.ToString() ?? "-"} dying {screen.Wish.IsDying}, " +
             $"{stock.Sources.Count} stacks at hand, {stock.FreeCells} cells free, picked {string.Join(",", plan.Picks.Select(pick => $"{pick.Item.ConfigId}x{(pick.IsOneUnit ? 1 : pick.Item.Count)}"))}, " +
             $"missing [{string.Join(",", plan.Missing)}], stop {plan.Stop}, failed {failed}");

        var names = new Dictionary<int, string>(screen.Names);

        if (!names.ContainsKey(FillPlan.FoodCategory)) names[FillPlan.FoodCategory] = Texts.Food.Pick(isChinese);

        _toasts.Show(Texts.Outcome(plan, failed, names, isChinese), failed > 0 || plan.Picks.Count == 0, isChinese);
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
            read = NeedReader.Read();
        }
        catch (Exception ex)
        {
            // A game update that renames the clock's fields would fail here every few seconds; one line is enough.
            _hasWarningsFailed = true;
            _log("reading the neighbor's state failed, warnings are off until the next start: " + ex);

            return;
        }

        if (read is not { } state) return;

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
