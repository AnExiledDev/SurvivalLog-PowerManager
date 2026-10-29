using System;
using System.IO;
using System.Threading;
using BepInEx.Logging;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts
/// (LogOutput.log is overwritten on the next launch). The file is written by a worker thread, so a frame that logs
/// (a refuel or a switch) never waits on the disk; the worker never writes to the BepInEx console.
/// </summary>
internal sealed class PowerLog
{
    private const int QuitWaitMilliseconds = 2000;

    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly object _gate = new();
    private readonly LogLines _lines = new();
    private readonly AutoResetEvent _wake = new(false);

    public PowerLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;

        try
        {
            new Thread(WriteLoop) { IsBackground = true, Name = "Power Manager log" }.Start();
            AppDomain.CurrentDomain.ProcessExit += WriteOnQuit;
        }
        catch (Exception)
        {
            // Without the worker the lines wait in memory; the BepInEx log still has every one.
        }
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
            _lines.Add(DateTime.UtcNow, message);
            _wake.Set();
        }
        catch (Exception)
        {
            // A line lost from the file copy costs nothing else.
        }
    }

    private void WriteLoop()
    {
        try
        {
            while (true)
            {
                _wake.WaitOne();
                WriteWaiting();
            }
        }
        catch (Exception)
        {
            // The file copy stops; the BepInEx log still has every line.
        }
    }

    /// <summary>The game is closing: the lines still waiting are written, giving up after a short wait for a write in progress.</summary>
    private void WriteOnQuit(object? sender, EventArgs e)
    {
        if (!Monitor.TryEnter(_gate, QuitWaitMilliseconds)) return;

        try
        {
            WriteWaitingLocked();
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    private void WriteWaiting()
    {
        lock (_gate) WriteWaitingLocked();
    }

    /// <summary>The file is opened shared for reading, so the log can be tailed while the game runs.</summary>
    private void WriteWaitingLocked()
    {
        try
        {
            var text = _lines.Take();

            if (text == null) return;

            using var file = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(file);

            writer.Write(text);
        }
        catch (Exception)
        {
            // A locked or read-only file costs the file copy, never the plugin.
        }
    }
}
