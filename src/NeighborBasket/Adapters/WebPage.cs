using System;
using System.IO;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's pages sit in. It puts
/// the basket station over the basket screen, adds the phone app, and shows a toast when the Toolkit cannot. The web
/// view can reload at any time and lose the script, so it is checked for every couple of seconds and put back when the
/// page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "NeighborBasket.page.js";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run("window.__slneighborbasket||" + NoScriptReply);
    }

    public void Install()
    {
        if (Run(Script())) _log("page script sent to the web UI");
    }

    /// <summary>
    /// A toast over the game; the script travels with it, so a page that just reloaded still shows it. An empty one only
    /// frees the page's buttons, which a page without the script has no use for, so it goes alone.
    /// </summary>
    public void Status(string text) => Run((text.Length > 0 ? Script() + "\n;" : "") + "window.__slneighborbasket&&window.__slneighborbasket.status(" + JsText.Literal(text) + ");");

    /// <summary>Which buttons and whether the phone app show, with their labels and tooltips in the game's language.</summary>
    public void Setup(bool showFill, bool showCallBack, bool showPutAway, bool showApp, bool isChinese) =>
        Run("window.__slneighborbasket&&window.__slneighborbasket.setup({" +
            "fill:" + Bool(showFill) + ",callBack:" + Bool(showCallBack) + ",putAway:" + Bool(showPutAway) + ",app:" + Bool(showApp) +
            ",fillLabel:" + JsText.Literal(Texts.FillButton.Pick(isChinese)) + ",fillTitle:" + JsText.Literal(Texts.FillTitle.Pick(isChinese)) +
            ",callBackLabel:" + JsText.Literal(Texts.CallBackButton.Pick(isChinese)) + ",callBackTitle:" + JsText.Literal(Texts.CallBackTitle.Pick(isChinese)) +
            ",putAwayLabel:" + JsText.Literal(Texts.PutAwayButton.Pick(isChinese)) + ",putAwayTitle:" + JsText.Literal(Texts.PutAwayTitle.Pick(isChinese)) +
            ",appLabel:" + JsText.Literal(Texts.AppTile.Pick(isChinese)) +
            ",stationLabel:" + JsText.Literal(Texts.StationButton.Pick(isChinese)) + ",gameLabel:" + JsText.Literal(Texts.GameScreen.Pick(isChinese)) + "})");

    /// <summary>The phone app's data, as AppView builds it.</summary>
    public void App(string json) => Run("window.__slneighborbasket&&window.__slneighborbasket.app(" + json + ")");

    /// <summary>The alert line on the game's Rescue panel; "null" takes it away.</summary>
    public void Hud(string json) => Run("window.__slneighborbasket&&window.__slneighborbasket.hud(" + json + ")");

    /// <summary>The basket station's data, as StationView builds it; false turns the station off.</summary>
    public void Station(string json) => Run("window.__slneighborbasket&&window.__slneighborbasket.station(" + json + ")");

    private static string Bool(bool value) => value ? "true" : "false";

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
