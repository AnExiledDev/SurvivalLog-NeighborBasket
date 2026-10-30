using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// page.js on a fake HUD page (CoreUI1): the basket line shows in the top left with or without an aid event, on the
/// neighbor's Rescue panel while there is one and in a panel of its own under the event list otherwise.
/// </summary>
public class HudLineTests
{
    private const string Basket = "{ text: 'In the basket: Apple x2', title: 'In her basket', items: ['Apple x2'] }";
    private const string Alert = "text: 'Trouble runs out in 8 h', tone: 'warn', more: '', title: 'Neighbor alerts', all: [{ text: 'Her trouble runs out in 8 h.', tone: 'warn' }]";

    private readonly Engine _engine = new();

    public HudLineTests()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        _engine.Execute(Resource("NeighborBasket.page.js"));
        _engine.Execute("var frame = openHud(); var doc = frame.contentDocument; var top = doc.querySelector('.top-left-panel');");
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string js) => _engine.Evaluate(js).ToString();

    [Fact]
    public void With_no_aid_event_the_basket_line_sits_in_its_own_panel_right_under_the_event_list()
    {
        Run("window.__slneighborbasket.hud({ basket: " + Basket + " });");

        Assert.Equal("event-panel | slnb-hud-basket-panel | forecast-panel", Text("hudChildren(top)"));
        Assert.Equal("In the basket: Apple x2", Text("doc.getElementById('slnb-hud-basket').querySelector('.slnb-hud-text').textContent"));
        Assert.Equal("Apple x2", Text("doc.getElementById('slnb-hud-basket').querySelector('.slnb-hud-tip').children[1].textContent"));
        Assert.True(_engine.Evaluate("doc.getElementById('slnb-hud-basket').hasAttribute('data-interactive')").AsBoolean());
    }

    [Fact]
    public void When_an_aid_event_starts_the_basket_line_moves_onto_the_rescue_panel_after_the_alert()
    {
        Run("window.__slneighborbasket.hud({ basket: " + Basket + " }); var rescue = showRescue(doc); tick();");
        Run("window.__slneighborbasket.hud({ " + Alert + ", basket: " + Basket + " });");

        Assert.Null(_engine.Evaluate("doc.getElementById('slnb-hud-basket-panel')").ToObject());
        Assert.Equal(" | slnb-hud-alert | slnb-hud-basket", Text("hudChildren(rescue)"));
    }

    [Fact]
    public void When_the_aid_event_ends_the_basket_line_goes_back_under_the_event_list()
    {
        Run("var rescue = showRescue(doc); window.__slneighborbasket.hud({ " + Alert + ", basket: " + Basket + " });");
        Run("top.removeChild(rescue); tick();");

        Assert.Equal("event-panel | slnb-hud-basket-panel | forecast-panel", Text("hudChildren(top)"));
        Assert.Null(_engine.Evaluate("doc.getElementById('slnb-hud-alert')").ToObject());
    }

    [Fact]
    public void An_emptied_basket_takes_the_line_and_its_panel_away()
    {
        Run("window.__slneighborbasket.hud({ basket: " + Basket + " }); window.__slneighborbasket.hud(null);");

        Assert.Equal("event-panel | forecast-panel", Text("hudChildren(top)"));
    }

    [Fact]
    public void Once_placed_the_check_searches_nothing_and_draws_nothing_again()
    {
        Run("window.__slneighborbasket.hud({ basket: " + Basket + " }); tick(); var text = doc.getElementById('slnb-hud-basket').querySelector('.slnb-hud-text');");
        Run("var searches = countSearches(doc); tick(); tick();");

        Assert.Equal(0, (int)_engine.Evaluate("searches.count").AsNumber());
        Assert.True(_engine.Evaluate("text.isConnected").AsBoolean());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(HudLineTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
