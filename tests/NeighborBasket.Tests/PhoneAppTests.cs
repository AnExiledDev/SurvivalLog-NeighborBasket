using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// page.js exactly as the plugin embeds it, run in Jint on a fake root page. The game builds a new phone page each
/// time the phone opens and shows it two frames later; the Neighbor app must be on it by then, not on the next page
/// check. None of these tests run the page check unless they say so.
/// </summary>
public class PhoneAppTests
{
    private readonly Engine _engine = new();

    public PhoneAppTests()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        _engine.Execute(Resource("NeighborBasket.page.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private int Apps(string doc) => (int)_engine.Evaluate($"apps({doc})").AsNumber();

    /// <summary>The phone opening: Root makes the frame, the page arrives a frame later, and the game builds it.</summary>
    private void OpenPhone()
    {
        Run("var frame = openFrame(PHONE_URL); flush(); nextFrame();");
        Run("var doc = arrive(frame); nextFrame(); buildPhone(doc); flush();");
    }

    [Fact]
    public void A_phone_that_just_opened_has_the_app_before_the_next_paint_without_a_page_check()
    {
        OpenPhone();

        Assert.Equal(1, Apps("doc"));
    }

    [Fact]
    public void A_phone_page_that_is_already_built_when_it_arrives_gets_the_app_by_the_next_frame()
    {
        Run("var frame = openFrame(PHONE_URL); flush(); buildPhone(arrive(frame)); nextFrame();");

        Assert.Equal(1, Apps("frame.contentDocument"));
    }

    [Fact]
    public void Every_new_phone_page_gets_the_app_again()
    {
        OpenPhone();
        Run("frame.remove(); flush(); var again = openFrame(PHONE_URL); flush(); buildPhone(arrive(again)); nextFrame();");

        Assert.Equal(1, Apps("again.contentDocument"));
    }

    [Fact]
    public void A_phone_page_loaded_into_the_frame_again_is_watched_again()
    {
        OpenPhone();
        Run("navigate(frame, PHONE_URL + '?t=2'); flush(); var reloaded = arrive(frame); fireLoad(frame); flush(); buildPhone(reloaded); flush();");

        Assert.Equal(1, Apps("reloaded"));
    }

    [Fact]
    public void The_watcher_and_the_page_check_together_add_one_app()
    {
        OpenPhone();
        Run("tick(); flush(); nextFrame(); tick();");

        Assert.Equal(1, Apps("doc"));
    }

    // The game and other mods change the open phone page all the time. Once the app is in, a change must not search
    // the page, or every change on the phone pays for it.
    [Fact]
    public void Once_the_app_is_in_changes_on_the_phone_page_search_nothing()
    {
        OpenPhone();
        Run("var grid = doc.querySelector('.app-grid'); var searches = countSearches(doc);");
        Run("for (var i = 0; i < 20; i++) { grid.appendChild(doc.createElement('div')); flush(); } tick(); nextFrame();");

        Assert.Equal(0, (int)_engine.Evaluate("searches.count").AsNumber());
        Assert.Equal(1, Apps("doc"));
    }

    [Fact]
    public void An_app_the_game_takes_off_the_home_screen_goes_back_on()
    {
        OpenPhone();
        Run("doc.querySelector('.slnb-app').remove(); flush();");

        Assert.Equal(1, Apps("doc"));
        Assert.True(_engine.Evaluate("doc.querySelector('.slnb-app').parentNode === doc.querySelector('.app-grid')").AsBoolean());
    }

    // Exiled's Toolkit moves the app into the phone's dock; the app stays there and no second one goes in.
    [Fact]
    public void An_app_moved_elsewhere_on_the_phone_page_is_left_where_it_is()
    {
        OpenPhone();
        Run("var dock = doc.body.appendChild(doc.createElement('div')); dock.appendChild(doc.querySelector('.slnb-app')); flush(); tick();");

        Assert.Equal(1, Apps("doc"));
        Assert.True(_engine.Evaluate("doc.querySelector('.slnb-app').parentNode === dock").AsBoolean());
    }

    [Fact]
    public void A_new_app_name_from_the_settings_shows_on_the_open_phone()
    {
        OpenPhone();
        Run("window.__slneighborbasket.setup({ app: true, appLabel: 'Helper' }); flush();");

        Assert.Equal(1, Apps("doc"));
        Assert.Equal("Helper", _engine.Evaluate("doc.querySelector('.app-label').textContent").AsString());
    }

    [Fact]
    public void An_app_turned_off_and_on_again_comes_back()
    {
        OpenPhone();
        Run("window.__slneighborbasket.setup({ app: false }); flush(); tick();");
        Assert.Equal(0, Apps("doc"));

        Run("window.__slneighborbasket.setup({ app: true }); flush(); tick();");

        Assert.Equal(1, Apps("doc"));
    }

    // Each load of a phone frame starts a look for its page; a frame keeps only the newest, not one per load.
    [Fact]
    public void A_phone_frame_that_loads_again_keeps_one_look_for_its_page()
    {
        Run("var frame = openFrame(PHONE_URL); flush(); fireLoad(frame); fireLoad(frame); nextFrame();");

        Assert.Equal(1, (int)_engine.Evaluate("frameCallbacks.length").AsNumber());
    }

    [Fact]
    public void The_look_for_a_phone_page_that_never_comes_stops()
    {
        Run("var frame = openFrame(PHONE_URL); flush(); for (var i = 0; i < 200; i++) nextFrame();");

        Assert.Equal(0, (int)_engine.Evaluate("frameCallbacks.length").AsNumber());
    }

    // The station check runs every 150 ms. An inventory page the game has hidden must not make the browser work out
    // the layout each time.
    [Fact]
    public void A_hidden_inventory_page_costs_the_station_check_no_layout_read()
    {
        Run("var bag = openFrame('file:///WebUI/UI/BackpackUI/BackpackUI.html'); flush(); arrive(bag); bag.style.display = 'none'; var reads = countLayoutReads(bag); tick(); tick();");

        Assert.Equal(0, (int)_engine.Evaluate("reads.count").AsNumber());
    }

    [Fact]
    public void Other_pages_never_get_the_app()
    {
        Run("var frame = openFrame('file:///WebUI/UI/Bag/Bag.html'); flush(); buildPhone(arrive(frame)); nextFrame(); flush(); tick();");

        Assert.Equal(0, Apps("frame.contentDocument"));
    }

    [Fact]
    public void With_the_app_turned_off_the_phone_stays_as_the_game_made_it()
    {
        Run("window.__slneighborbasket.setup({ app: false });");
        OpenPhone();

        Assert.Equal(0, Apps("doc"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PhoneAppTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
