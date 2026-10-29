using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// The station's search box and the game's keyboard, with page.js run in Jint on a fake root page and inventory page.
/// The game sends typed keys to the web UI only while it is told a text box has focus, so the page tells the plugin
/// when the box takes the keyboard and when it lets go, the basket closing included.
/// </summary>
public class StationTypingTests
{
    private const string Focus = "slmod|neighborbasket|focus|";

    private const string Station =
        "{ labels: { cells: '{0}/{1}' }, items: [{ id: 7, n: 'Apple', w: 1, h: 1, q: 3, owner: 2, ic: 'apple.png' }], basket: [], " +
        "places: [{ id: 2, name: 'Chest' }], bag: 1, cols: 5, gridRows: 3, rows: [] }";

    private readonly Engine _engine = new();

    public StationTypingTests()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        _engine.Execute(Resource("NeighborBasket.page.js"));
        _engine.Execute("var frame = openBasket(); tick(); window.__slneighborbasket.station(" + Station + "); tick(); posted = [];");
    }

    private string Focuses() => _engine.Evaluate("posted.filter(function (m) { return m.indexOf('" + Focus + "') === 0; }).join(',')").AsString();

    [Fact]
    public void Clicking_into_the_search_asks_the_game_for_the_keyboard_and_clicking_out_gives_it_back()
    {
        _engine.Execute("focusSearch(frame);");

        Assert.Equal(Focus + "1", Focuses());

        _engine.Execute("blurSearch(frame); tick();");

        Assert.Equal(Focus + "1," + Focus + "0", Focuses());
    }

    [Fact]
    public void Typing_on_while_the_station_stays_open_asks_only_once()
    {
        _engine.Execute("focusSearch(frame); tick(); tick();");

        Assert.Equal(Focus + "1", Focuses());
    }

    [Fact]
    public void The_basket_closing_while_typing_gives_the_keyboard_back()
    {
        _engine.Execute("focusSearch(frame); frame.contentDocument.getElementById('app')._vnode.component.setupState.isBasket = false; tick();");

        Assert.Equal(Focus + "1," + Focus + "0", Focuses());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(StationTypingTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
