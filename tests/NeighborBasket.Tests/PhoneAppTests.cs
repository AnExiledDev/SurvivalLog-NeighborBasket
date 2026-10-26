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
