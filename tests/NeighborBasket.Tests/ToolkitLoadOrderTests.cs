using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// page.js with Exiled's Toolkit's page API (api.js) in either order. The game can run either script first, and the
/// web view can reload and run them again in the other order; the page must end up the same both ways, with nothing
/// the plugin sent in between lost.
/// </summary>
public class ToolkitLoadOrderTests
{
    private const string Basket = "{ text: 'In the basket: Apple x2', title: 'In her basket', items: ['Apple x2'] }";

    private readonly Engine _engine = new();

    public ToolkitLoadOrderTests()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private bool Is(string expression) => _engine.Evaluate(expression).AsBoolean();

    private void LoadPage() => Run(Resource("NeighborBasket.page.js"));

    private void LoadToolkit() => Run(Resource("ExiledToolkit.api.js"));

    private int Readies() => (int)_engine.Evaluate("posted.filter(function (m) { return m === 'slmod|neighborbasket|ready'; }).length").AsNumber();

    [Fact]
    public void With_the_Toolkit_first_the_page_says_ready_once_as_it_loads()
    {
        LoadToolkit();
        LoadPage();

        Assert.Equal(1, Readies());
    }

    [Fact]
    public void With_the_page_first_it_says_ready_once_the_Toolkit_has_loaded()
    {
        LoadPage();

        Assert.Equal(0, Readies());
        Assert.True(Is("!!window.__slneighborbasket"));

        LoadToolkit();

        Assert.Equal(1, Readies());
    }

    // The plugin can send the basket line between the two scripts; it must show once the Toolkit is there, without
    // waiting for a new send.
    [Fact]
    public void A_HUD_line_sent_before_the_Toolkit_loads_shows_when_it_does()
    {
        Run("var frame = openHud(); var doc = frame.contentDocument;");
        LoadPage();
        Run("window.__slneighborbasket.hud({ basket: " + Basket + " });");

        Assert.False(Is("!!doc.getElementById('slnb-hud-basket')"));

        LoadToolkit();

        Assert.True(Is("!!doc.getElementById('slnb-hud-basket')"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_station_opens_on_the_basket_whichever_script_ran_first(bool isToolkitFirst)
    {
        if (isToolkitFirst) LoadToolkit();
        LoadPage();
        if (!isToolkitFirst) LoadToolkit();

        Run("var frame = openBasket(); tick();");

        Assert.True(Is("!!stationIn(frame) && stationIn(frame).style.display !== 'none'"));
        Assert.True(Is("!!frame.contentDocument.getElementById('slnb-style')"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_phone_opened_after_both_scripts_gets_the_app_before_the_next_paint_whichever_ran_first(bool isToolkitFirst)
    {
        if (isToolkitFirst) LoadToolkit();
        LoadPage();
        if (!isToolkitFirst) LoadToolkit();

        Run("var frame = openFrame(PHONE_URL); flush(); nextFrame(); var doc = arrive(frame); nextFrame(); buildPhone(doc); flush();");

        Assert.Equal(1, (int)_engine.Evaluate("apps(doc)").AsNumber());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ToolkitLoadOrderTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
