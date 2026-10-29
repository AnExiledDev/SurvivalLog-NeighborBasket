using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// The basket station at the player's UI size from Exiled's Toolkit, with page.js run in Jint
/// on a fake root page and inventory page. The station fills the screen at any size, so the Toolkit is given a box as
/// big as its panes need to fit. The size multiplies every length inside the station, so the tip that follows the
/// mouse is placed on screen and written back in the station's own px.
/// </summary>
public class StationUiSizeTests
{
    // The Toolkit's side: it keeps what it was handed, and zoomTo draws each at a size the way it does.
    private const string FakeUi =
        "var handed = []; window.ExiledUi = { size: function (el, options) { handed.push({ el: el, options: options }); } };" +
        "function zoomTo(z) { handed.forEach(function (h) { h.el.style.zoom = String(z); if (h.options.onZoom) h.options.onZoom(z); }); }";

    private const string Station =
        "{ labels: { cells: '{0}/{1}' }, items: [{ id: 7, n: 'Apple', w: 1, h: 1, q: 3, owner: 2, ic: 'apple.png' }], basket: [], " +
        "places: [{ id: 2, name: 'Chest' }], bag: 1, cols: 5, gridRows: 3, rows: [] }";

    private readonly Engine _engine = new();

    private void Load(bool withToolkit)
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        if (withToolkit) _engine.Execute(FakeUi);
        _engine.Execute(Resource("NeighborBasket.page.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string expression) => _engine.Evaluate(expression).AsString();

    private bool Is(string expression) => _engine.Evaluate(expression).AsBoolean();

    private double Number(string expression) => _engine.Evaluate(expression).AsNumber();

    /// <summary>The basket opens and the plugin's first answer arrives; the tip is 200 x 60 in its own px.</summary>
    private void OpenStation(string data = Station)
    {
        Run("var frame = openBasket(); tick(); window.__slneighborbasket.station(" + data + ");");
        Run("var tip = stationIn(frame).querySelector('.nbs-tip'); tip.offsetWidth = 200; tip.offsetHeight = 60;");
    }

    [Fact]
    public void The_station_is_handed_once_with_its_fit_box()
    {
        Load(withToolkit: true);
        OpenStation();
        Run("tick(); tick();");

        Assert.Equal(1, Number("handed.length"));
        Assert.Equal(1, Number("handed.filter(function (h) { return h.options.mod === 'NeighborBasket'; }).length"));
        Assert.True(Is("handed[0].el === stationIn(frame)"));
        Assert.True(Is("handed[0].options.fit === stationIn(frame).querySelector('.nbs-fit')"));
    }

    [Fact]
    public void A_new_inventory_page_gets_its_station_handed_again()
    {
        Load(withToolkit: true);
        OpenStation();
        Run("frame.remove(); var again = openBasket(); tick();");

        Assert.Equal(2, Number("handed.length"));
        Assert.True(Is("handed[1].el === stationIn(again)"));
    }

    [Fact]
    public void Without_the_Toolkit_the_station_waits_in_the_list()
    {
        Load(withToolkit: false);
        OpenStation();

        Assert.Equal(1, Number("window.__exiledUiWait.length"));
        Assert.True(Is("window.__exiledUiWait[0][0] === stationIn(frame)"));
    }

    // More basket rows need more room, so the box the Toolkit fits grows with them and a short screen gets a smaller size.
    [Fact]
    public void A_basket_with_more_rows_makes_the_fit_box_taller()
    {
        Load(withToolkit: true);
        OpenStation(Station.Replace("gridRows: 3", "gridRows: 5"));

        Assert.Equal("calc(826px + 6vh)", Text("stationIn(frame).querySelector('.nbs-fit').style.height"));
    }

    [Fact]
    public void A_press_answer_draws_nothing_and_hands_nothing_to_the_toolkit()
    {
        Load(withToolkit: true);
        Run("window.__slneighborbasket.status('Zq9 press answer');");

        Assert.Equal(0, Number("handed.length"));
        Assert.False(Is("!!document.getElementById('slnb-toast')"));
        Assert.False(Is("document.body.textContent.indexOf('Zq9 press answer') >= 0"));
    }

    // At double size the tip is 400 x 120 on screen and sits 28 px from the mouse; written in the station's own px,
    // every number is halved.
    [Fact]
    public void At_a_bigger_size_the_tip_follows_the_mouse_on_screen()
    {
        Load(withToolkit: true);
        OpenStation();
        Run("zoomTo(2); hoverStack(frame, 100, 100);");

        Assert.Equal("64px", Text("tip.style.left"));
        Assert.Equal("64px", Text("tip.style.top"));
    }

    [Fact]
    public void At_a_bigger_size_the_tip_turns_back_at_the_screen_edge_by_its_size_on_screen()
    {
        Load(withToolkit: true);
        OpenStation();
        Run("zoomTo(2); hoverStack(frame, 1800, 1000);");

        // 1800 - 400 - 28 = 1372 and 1000 - 120 - 28 = 852 on screen.
        Assert.Equal("686px", Text("tip.style.left"));
        Assert.Equal("426px", Text("tip.style.top"));
    }

    // The tip is measured once per stack; a new size while it shows must measure it again, or it is placed by its old
    // size and runs off the screen.
    [Fact]
    public void A_new_size_while_the_tip_shows_hides_it_and_measures_it_again()
    {
        Load(withToolkit: true);
        OpenStation();
        Run("hoverStack(frame, 1800, 1000); zoomTo(2);");

        Assert.Equal("none", Text("tip.style.display"));

        Run("hoverStack(frame, 1800, 1000);");

        Assert.Equal("686px", Text("tip.style.left"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(StationUiSizeTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
