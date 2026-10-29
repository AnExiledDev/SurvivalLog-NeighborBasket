using System;
using System.IO;
using Jint;
using Xunit;

namespace NeighborBasket.Tests;

/// <summary>
/// Clicks on the basket station, with page.js run in Jint on a fake root page and inventory page. Messages are Exiled's
/// Toolkit toasts shown by the plugin, so the page asks the plugin instead of drawing a notice of its own.
/// </summary>
public class StationClickTests
{
    private const string Forbidden =
        "{ labels: { cells: '{0}/{1}' }, items: [{ id: 7, n: 'Rope', w: 1, h: 1, q: 3, owner: 2, ic: 'rope.png', no: true }], basket: [], " +
        "places: [{ id: 2, name: 'Chest' }], bag: 1, cols: 5, gridRows: 3, rows: [] }";

    private readonly Engine _engine = new();

    [Fact]
    public void A_stack_she_will_not_take_is_left_to_the_plugin_to_say_so()
    {
        _engine.Execute(Resource("NeighborBasket.Tests.PhoneFake.js"));
        _engine.Execute(Resource("NeighborBasket.page.js"));
        _engine.Execute("var frame = openBasket(); tick(); window.__slneighborbasket.station(" + Forbidden + ");");

        _engine.Execute("var station = stationIn(frame); var row = station.querySelector('[data-add]'); " +
            "station.listeners.click.forEach(function (callback) { callback({ target: row, shiftKey: false, stopPropagation: function () {} }); });");

        Assert.True(_engine.Evaluate("posted.indexOf('slmod|neighborbasket|add|7|one') >= 0").AsBoolean());
        Assert.False(_engine.Evaluate("!!document.getElementById('slnb-toast')").AsBoolean());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(StationClickTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
