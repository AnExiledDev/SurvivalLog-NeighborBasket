using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BepInEx.Logging;
using NeighborBasket.Core;

namespace NeighborBasket.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts
/// (LogOutput.log is overwritten on the next launch). The file is written on a background thread, so a line logged
/// on a game frame never waits for the disk; each line keeps the time it was logged.
/// </summary>
internal sealed class BasketLog
{
    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly ConcurrentQueue<(DateTime At, string Message)> _lines = new();
    private readonly AutoResetEvent _wake = new(false);
    private StreamWriter? _file;

    public BasketLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;

        new Thread(WriteLoop) { IsBackground = true, Name = "NeighborBasket log" }.Start();
    }

    /// <summary>Never throws: a logger that can fail would take down whatever it was asked to report on.</summary>
    public void Write(string message)
    {
        try
        {
            if (HookFailure.Is(message)) _bepinex.LogError(message);
            else _bepinex.LogInfo(message);
        }
        catch (Exception)
        {
            // The file copy below still has a chance.
        }

        try
        {
            // UtcNow reads no time zone; the writer turns it into local time.
            _lines.Enqueue((DateTime.UtcNow, message));
            _wake.Set();
        }
        catch (Exception)
        {
            // The BepInEx log already has the line.
        }
    }

    private void WriteLoop()
    {
        while (true)
        {
            try
            {
                _wake.WaitOne();

                while (_lines.TryDequeue(out var line)) AppendToFile($"{line.At.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} {line.Message}");

                // Flushed after each batch, so a crash loses at most the lines of the moment.
                _file?.Flush();
            }
            catch (Exception)
            {
                CloseFile();
            }
        }
    }

    /// <summary>One writer stays open so a busy moment is not paying for an open and close per line.</summary>
    private void AppendToFile(string line)
    {
        try
        {
            _file ??= OpenFile();
            _file.WriteLine(line);
        }
        catch (Exception)
        {
            // The BepInEx log already has the line; the writer is dropped so the next line tries a fresh one.
            CloseFile();
        }
    }

    /// <summary>Shared for reading so the log can be tailed while the game runs.</summary>
    private StreamWriter OpenFile() =>
        new(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

    private void CloseFile()
    {
        try
        {
            _file?.Dispose();
        }
        catch (Exception)
        {
            // A writer that failed to write can fail to close too; it is being thrown away either way.
        }

        _file = null;
    }
}
