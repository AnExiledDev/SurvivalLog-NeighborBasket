using System;
using System.IO;
using System.Runtime.CompilerServices;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// Shows a toast through Exiled's Toolkit when it is installed, and otherwise on the page's own toast. Only
/// <see cref="ShowWithToolkit"/> names a Toolkit type, so the mod loads without it.
/// </summary>
internal sealed class Toasts
{
    private readonly WebPage _page;
    private readonly Action<string> _log;
    private bool _isBroken;
    private bool _hasReportedFailure;

    public Toasts(WebPage page, Action<string> log)
    {
        _page = page;
        _log = log;
    }

    public void Show(string text, bool isWarning, bool isChinese)
    {
        // An empty page status still frees the page's buttons for the next press.
        var isShown = !_isBroken && OptionalLive.IsToolkitInstalled && TryToolkit(text, isWarning, isChinese);

        _page.Status(isShown ? "" : text);
    }

    private bool TryToolkit(string text, bool isWarning, bool isChinese)
    {
        try
        {
            ShowWithToolkit(text, isWarning, isChinese);

            return true;
        }
        catch (Exception ex) when (ex is TypeLoadException or MissingMethodException or FileNotFoundException or FileLoadException)
        {
            _isBroken = true;
            ReportOnce("Exiled's Toolkit could not show a toast, so the page's own toast is used: " + ex.Message);
        }
        catch (Exception ex)
        {
            ReportOnce("could not show a toast (reported once): " + ex.Message);
        }

        return false;
    }

    // Kept in its own method so the Toolkit's types are only looked up when it is installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ShowWithToolkit(string text, bool isWarning, bool isChinese) =>
        ExiledToolkit.Toasts.Show(new ExiledToolkit.ToastRequest(Texts.Source.Pick(isChinese), text)
        {
            Kind = isWarning ? ExiledToolkit.ToastKind.Warning : ExiledToolkit.ToastKind.Success,
        });

    private void ReportOnce(string message)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;
        _log(message);
    }
}
