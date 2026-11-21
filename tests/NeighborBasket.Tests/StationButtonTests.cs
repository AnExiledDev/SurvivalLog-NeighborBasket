using System;
using System.IO;
using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// The Basket station button that brings the station back from the game's screen, with page.js run in Jint on a fake
/// root page and inventory page. The game draws its money box in the top right corner, so the button sits in the
/// basket's header beside its name.
/// </summary>
public class StationButtonTests
{
    private const string Answer =
        "{ labels: { station: 'Basket station', game: 'Game screen', cells: '{0}/{1}' }, items: [], basket: [], places: [], bag: 1, cols: 5, gridRows: 3, rows: [] }";

    private readonly Engine _engine = new();

    public StationButtonTests()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("NeighborBasket.page.js"));
        _engine.Execute("var frame = openBasket(); var doc = frame.contentDocument; tick(); window.__slneighborbasket.station(" + Answer + ");");
    }

    // The bug: the button was fixed to the top right corner of the screen and covered the player's money.
    [Fact]
    public void Going_to_the_game_screen_puts_the_button_beside_the_basket_name()
    {
        _engine.Execute("var header = drawBasketPanel(frame); goToGameScreen(frame); tick(); var open = doc.getElementById('slnb-station-open');");

        Assert.True(_engine.Evaluate("open.parentNode === header").AsBoolean());
        Assert.Equal("block", _engine.Evaluate("open.style.display").AsString());
        Assert.Equal("Basket station", _engine.Evaluate("open.textContent").AsString());
    }

    [Fact]
    public void Before_the_game_draws_the_basket_panel_the_button_waits_then_moves_beside_the_name()
    {
        _engine.Execute("goToGameScreen(frame); tick(); var open = doc.getElementById('slnb-station-open');");

        Assert.True(_engine.Evaluate("open.parentNode === doc.body").AsBoolean());
        Assert.Equal("block", _engine.Evaluate("open.style.display").AsString());

        _engine.Execute("var header = drawBasketPanel(frame); tick();");

        Assert.True(_engine.Evaluate("doc.getElementById('slnb-station-open').parentNode === header").AsBoolean());
    }

    // The game sends Unity the spots of its buttons only after a style changes. A button moved while shown would keep
    // its old spot, and clicks on it would fall through to the game.
    [Fact]
    public void A_button_that_moves_beside_the_name_is_hidden_and_shown_again_so_the_game_hears_where_it_went()
    {
        _engine.Execute("goToGameScreen(frame); tick(); var open = doc.getElementById('slnb-station-open');");
        _engine.Execute("var writes = []; var shown = open.style.display; Object.defineProperty(open.style, 'display', { get: function () { return shown; }, set: function (v) { shown = v; writes.push(v); } });");
        _engine.Execute("var header = drawBasketPanel(frame); tick();");

        Assert.True(_engine.Evaluate("open.parentNode === header").AsBoolean());
        Assert.Equal("none,block", _engine.Evaluate("writes.join(',')").AsString());
    }

    // The station's three panes need about 1350 px side by side. On a narrower screen the Toolkit draws it smaller,
    // so the take-from list keeps room for item names.
    [Fact]
    public void The_station_asks_the_toolkit_to_draw_it_smaller_on_a_screen_too_narrow_for_it()
    {
        _engine.Execute("var entry = (window.__exiledUiWait || []).filter(function (e) { return e[0] === stationIn(frame); })[0];");

        Assert.True(_engine.Evaluate("!!entry && entry[1].fitWidth === true").AsBoolean());
        Assert.True(_engine.Evaluate("entry[1].fit === stationIn(frame).querySelector('.nbs-fit')").AsBoolean());
    }

    // The game redraws its header whenever it likes, and the button inside goes with the old one. The player still
    // needs a way back to the station.
    [Fact]
    public void A_redrawn_header_gets_a_button_that_still_brings_the_station_back()
    {
        _engine.Execute("var header = drawBasketPanel(frame); goToGameScreen(frame); tick();");
        _engine.Execute("var panel = doc.querySelector('.is-basket-panel'); panel.parentNode.removeChild(panel); var redrawn = drawBasketPanel(frame); tick();");
        _engine.Execute("var open = doc.getElementById('slnb-station-open');");

        Assert.True(_engine.Evaluate("!!open && open.parentNode === redrawn").AsBoolean());
        Assert.Equal("block", _engine.Evaluate("open.style.display").AsString());

        _engine.Execute("open.listeners.click.forEach(function (callback) { callback({ target: open, stopPropagation: function () {} }); }); tick();");

        Assert.Equal("flex", _engine.Evaluate("stationIn(frame).style.display").AsString());
        Assert.Equal("none", _engine.Evaluate("open.style.display").AsString());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(StationButtonTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
