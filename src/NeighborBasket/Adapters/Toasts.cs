using System;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>Shows a message as an Exiled's Toolkit toast, then frees the page's buttons for the next press.</summary>
internal sealed class Toasts
{
    private readonly WebPage _page;
    private readonly Action<string> _log;

    public Toasts(WebPage page, Action<string> log)
    {
        _page = page;
        _log = log;
    }

    public void Show(string text, bool isWarning, bool isChinese)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var request = new ExiledToolkit.ToastRequest(Texts.Source.Pick(isChinese), text)
            {
                Kind = isWarning ? ExiledToolkit.ToastKind.Warning : ExiledToolkit.ToastKind.Success,
            };

            if (ExiledToolkit.Toasts.Show(request).IsNone) _log("a toast was not shown: " + text);
        }

        _page.Status();
    }
}
