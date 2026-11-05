using System;
using ExiledToolkit.Web;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's pages sit in. It puts
/// the basket station over the basket screen, adds the phone app and the HUD lines. The web view can reload at any
/// time and lose the script, so Exiled's Toolkit checks for it every couple of seconds and the session puts it back
/// when the page says it is missing.
/// </summary>
internal sealed class BasketPage
{
    private const string ScriptResource = "NeighborBasket.page.js";
    private const string Global = "window.__slneighborbasket";

    private readonly WebPage _page;

    public BasketPage(Action<string> log)
    {
        _page = new WebPage(typeof(BasketPage).Assembly, ScriptResource, Global, PageCommand.Prefix + "noscript", log);
    }

    public void KeepInstalled(double now) => _page.KeepInstalled(now);

    /// <summary>Puts the script in the page.</summary>
    public void Install() => _page.Install();

    /// <summary>A press is answered: frees the page's buttons. What the press did is an Exiled's Toolkit toast.</summary>
    public void Status() => CallIfInstalled("status()");

    /// <summary>Which buttons and whether the phone app show, with their labels and tooltips in the game's language.</summary>
    public void Setup(bool showFill, bool showCallBack, bool showPutAway, bool showApp, bool isChinese) =>
        CallIfInstalled("setup({" +
            "fill:" + Bool(showFill) + ",callBack:" + Bool(showCallBack) + ",putAway:" + Bool(showPutAway) + ",app:" + Bool(showApp) +
            ",fillLabel:" + JsText.Literal(Texts.FillButton.Pick(isChinese)) + ",fillTitle:" + JsText.Literal(Texts.FillTitle.Pick(isChinese)) +
            ",callBackLabel:" + JsText.Literal(Texts.CallBackButton.Pick(isChinese)) + ",callBackTitle:" + JsText.Literal(Texts.CallBackTitle.Pick(isChinese)) +
            ",putAwayLabel:" + JsText.Literal(Texts.PutAwayButton.Pick(isChinese)) + ",putAwayTitle:" + JsText.Literal(Texts.PutAwayTitle.Pick(isChinese)) +
            ",appLabel:" + JsText.Literal(Texts.AppTile.Pick(isChinese)) +
            ",stationLabel:" + JsText.Literal(Texts.StationButton.Pick(isChinese)) + ",gameLabel:" + JsText.Literal(Texts.GameScreen.Pick(isChinese)) + "})");

    /// <summary>The phone app's data, as AppView builds it.</summary>
    public void App(string json) => CallIfInstalled("app(" + json + ")");

    /// <summary>The alert line on the game's Rescue panel; "null" takes it away.</summary>
    public void Hud(string json) => CallIfInstalled("hud(" + json + ")");

    /// <summary>The basket station's data, as StationView builds it; false turns the station off.</summary>
    public void Station(string json) => CallIfInstalled("station(" + json + ")");

    private static string Bool(bool value) => value ? "true" : "false";

    // A call to a page without the script does nothing; the probe finds the missing script and asks for one install,
    // where the Toolkit's Call would ask once for every call made before the script is back.
    private void CallIfInstalled(string expression) => _page.Run(Global + "&&" + Global + "." + expression);
}
