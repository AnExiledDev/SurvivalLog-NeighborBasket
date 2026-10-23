using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// The gift log for the loaded save: one text file per save under BepInEx/config, kept to the newest entries. Fed by a
/// poll of the basket and by the copied thoughts; a save change loads that save's file and starts watching afresh.
/// </summary>
internal sealed class GiftJournal
{
    private const int KeepLines = 1000;
    private const int TrimAtLines = 1200;

    private readonly string _folder;
    private readonly Action<string> _log;
    private readonly List<LogEntry> _entries = new();
    private GiftWatch _watch = new();
    private string? _saveKey;
    private bool _hasReportedWriteFailure;

    public GiftJournal(string folder, Action<string> log)
    {
        _folder = folder;
        _log = log;
    }

    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>Switches to the loaded save's log; false when no save is loaded.</summary>
    public bool FollowSave()
    {
        var key = SaveSlot.CurrentKey();

        if (key == _saveKey) return key != null;

        _saveKey = key;
        _watch = new GiftWatch();
        _entries.Clear();

        while (MonologueHook.TryTake(out _))
        {
        }

        if (key != null) Load();

        return key != null;
    }

    public void See(BasketLook look)
    {
        while (MonologueHook.TryTake(out var text)) Add(new LogEntry(LogKind.Said, look.Day, look.Hour, text));

        foreach (var entry in _watch.See(look))
        {
            Add(entry);
            _log($"gift log: {entry.Kind} day {entry.Day} {entry.Hour}:00 [{entry.Text}] progress {entry.Progress:+0;-0;0}, affinity {entry.Affinity:+0;-0;0}");
        }
    }

    private string FilePath() => Path.Combine(_folder, Safe(_saveKey ?? "none") + ".txt");

    private void Load()
    {
        try
        {
            var path = FilePath();

            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path);

            _entries.AddRange(lines.Select(LogEntry.Parse).OfType<LogEntry>().TakeLast(KeepLines));

            if (lines.Length > TrimAtLines) File.WriteAllLines(path, _entries.Select(entry => entry.ToLine()));
        }
        catch (Exception ex)
        {
            _log("reading the gift log failed, it starts empty for this save: " + ex.Message);
        }
    }

    private void Add(LogEntry entry)
    {
        _entries.Add(entry);

        if (_entries.Count > KeepLines) _entries.RemoveRange(0, _entries.Count - KeepLines);

        try
        {
            Directory.CreateDirectory(_folder);
            File.AppendAllText(FilePath(), entry.ToLine() + Environment.NewLine);
        }
        catch (Exception ex)
        {
            if (!_hasReportedWriteFailure) _log("writing the gift log failed (reported once): " + ex.Message);

            _hasReportedWriteFailure = true;
        }
    }

    private static string Safe(string name)
    {
        var bad = Path.GetInvalidFileNameChars();

        return new string(name.Select(character => bad.Contains(character) || character is '/' or '\\' or ':' ? '_' : character).ToArray());
    }
}
