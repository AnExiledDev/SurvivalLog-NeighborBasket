using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NeighborBasket.Core;

public enum LogKind
{
    Sent,
    Returned,
    Said,
}

/// <summary>
/// One line of the gift log. Progress and Affinity are what a send gained; they are 0 for the other kinds. Text is the
/// basket's contents for a send or a return, and her words for a thought.
/// </summary>
public sealed record LogEntry(LogKind Kind, int Day, int Hour, string Text, int Progress = 0, int Affinity = 0)
{
    /// <summary>One line of the log file: tab-separated, with tabs, line breaks and backslashes escaped in the text.</summary>
    public string ToLine() => string.Join("\t", Kind.ToString(), Num(Day), Num(Hour), Num(Progress), Num(Affinity), Escape(Text));

    /// <summary>A line written by ToLine, or null when it is not one (a damaged or foreign line is skipped, not fatal).</summary>
    public static LogEntry? Parse(string? line)
    {
        var parts = line?.Split('\t');

        if (parts is not { Length: 6 }) return null;
        if (!Enum.TryParse<LogKind>(parts[0], out var kind) || !Enum.IsDefined(typeof(LogKind), kind)) return null;
        if (!Int(parts[1], out var day) || !Int(parts[2], out var hour) || !Int(parts[3], out var progress) || !Int(parts[4], out var affinity)) return null;

        return new LogEntry(kind, day, hour, Unescape(parts[5]), progress, affinity);
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private static string Escape(string text) => (text ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");

    private static string Unescape(string text)
    {
        var plain = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length)
            {
                plain.Append(text[i]);

                continue;
            }

            var next = text[++i];
            plain.Append(next switch { 't' => '\t', 'n' => '\n', _ => next });
        }

        return plain.ToString();
    }
}

/// <summary>What one poll sees: whether the basket is out, what is in it, and the rescue's progress and affinity.</summary>
public sealed record BasketLook(bool IsAway, string Items, int Progress, int Affinity, int Day, int Hour);

/// <summary>
/// Turns polls of the basket into log entries. Leaving home is a send, coming home is a return. Each entry is made on
/// the poll after the change, because the game settles a send's progress and fills a returned basket a moment after
/// the basket changes state. A send lists what the basket held on the poll before it left.
/// </summary>
public sealed class GiftWatch
{
    private BasketLook? _last;
    private BasketLook? _beforeSend;
    private bool _isReturnPending;

    public IReadOnlyList<LogEntry> See(BasketLook look)
    {
        var made = new List<LogEntry>();

        if (_beforeSend is { } before)
        {
            _beforeSend = null;
            made.Add(new LogEntry(LogKind.Sent, before.Day, before.Hour, before.Items, look.Progress - before.Progress, look.Affinity - before.Affinity));
        }
        else if (_isReturnPending)
        {
            _isReturnPending = false;

            if (!look.IsAway) made.Add(new LogEntry(LogKind.Returned, look.Day, look.Hour, look.Items));
        }

        if (_last is { } last && last.IsAway != look.IsAway)
        {
            if (look.IsAway) _beforeSend = last with { Day = look.Day, Hour = look.Hour };
            else _isReturnPending = true;
        }

        _last = look;

        return made;
    }
}

/// <summary>The rescue's standing, for the top of the phone app.</summary>
public sealed record Tracker(int Affinity, int EndingAt, int Progress, int ProgressMax, int Streak, int StreakDays, int StrugglesLeft);

/// <summary>
/// Builds the phone app's data: her status (the basket card's rows plus the struggles left) and the log grouped by day,
/// newest first. Without a live rescue line it falls back to the saved tracker alone.
/// </summary>
public static class AppView
{
    public const int MaxEntries = 200;

    public static string Json(NeedState? need, Tracker? tracker, string? troubleName, int warnHours, IReadOnlyList<LogEntry> entries, bool isChinese)
    {
        var json = new StringBuilder("{");

        json.Append("\"title\":").Append(JsText.Literal(Texts.AppTitle.Pick(isChinese)));
        json.Append(",\"back\":").Append(JsText.Literal(Texts.AppBack.Pick(isChinese)));
        json.Append(",\"empty\":").Append(JsText.Literal(Texts.AppEmpty.Pick(isChinese)));
        json.Append(",\"rows\":[");
        json.Append(string.Join(",", Rows(need, tracker, troubleName, warnHours, isChinese).Select(CardView.RowJson)));
        json.Append("],\"days\":[");

        var days = entries.Reverse().Take(MaxEntries).GroupBy(entry => entry.Day);

        json.Append(string.Join(",", days.Select(day => "{\"label\":" + JsText.Literal(Texts.AppDay.Say(isChinese, day.Key)) + ",\"entries\":[" + string.Join(",", day.Select(entry => Entry(entry, isChinese))) + "]}")));

        return json.Append("]}").ToString();
    }

    private static IEnumerable<CardView.CardRow> Rows(NeedState? need, Tracker? tracker, string? troubleName, int warnHours, bool isChinese)
    {
        if (need is { IsActive: true })
        {
            foreach (var row in CardView.Rows(need, tracker, troubleName, warnHours, isChinese)) yield return row;

            if (tracker is { } live) yield return StrugglesLeft(live, isChinese);

            yield break;
        }

        if (tracker is not { } t)
        {
            yield return new CardView.CardRow(Texts.AppNoRescue.Pick(isChinese), "");

            yield break;
        }

        yield return new CardView.CardRow(Texts.AppAffinity.Pick(isChinese), Texts.AppAffinityValue.Say(isChinese, t.Affinity, t.EndingAt));
        yield return new CardView.CardRow(Texts.AppProgress.Pick(isChinese), t.Progress + " / " + t.ProgressMax);

        if (t.StreakDays > 0) yield return new CardView.CardRow(Texts.AppStreak.Pick(isChinese), t.Streak + " / " + t.StreakDays);

        yield return StrugglesLeft(t, isChinese);
    }

    private static CardView.CardRow StrugglesLeft(Tracker tracker, bool isChinese) =>
        new(Texts.AppStrugglesLeft.Pick(isChinese), Math.Max(0, tracker.StrugglesLeft).ToString(CultureInfo.InvariantCulture));

    private static string Entry(LogEntry entry, bool isChinese)
    {
        var kind = entry.Kind switch
        {
            LogKind.Sent => Texts.AppSent,
            LogKind.Returned => Texts.AppReturned,
            _ => Texts.AppSaid,
        };
        var note = entry.Kind == LogKind.Sent ? Texts.AppGained.Say(isChinese, Signed(entry.Progress), Signed(entry.Affinity)) : "";
        var text = string.IsNullOrWhiteSpace(entry.Text) ? Texts.AppNothing.Pick(isChinese) : entry.Text;

        return "{\"kind\":" + JsText.Literal(entry.Kind.ToString().ToLowerInvariant()) +
               ",\"label\":" + JsText.Literal(kind.Pick(isChinese)) +
               ",\"time\":" + JsText.Literal(entry.Hour.ToString("00", CultureInfo.InvariantCulture) + ":00") +
               ",\"text\":" + JsText.Literal(text) +
               ",\"note\":" + JsText.Literal(note) + "}";
    }

    private static string Signed(int value) => (value >= 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);
}
