using System;
using System.IO;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The device priorities file beside the mod's config, keyed by save (the game's save has no room for a mod's data).
/// It follows the loaded save: a new save's marks are read into the run's book, and a changed book is written back.
/// A write goes to a temporary file first and is moved into place, so a crash mid-write cannot leave half a file.
/// </summary>
internal sealed class DeviceStore
{
    private enum State
    {
        NoSave,
        Unreadable,
        Ready,
    }

    private readonly string _path;
    private readonly Action<string> _log;
    private string? _save;
    private bool _hasFollowed;
    private State _state = State.NoSave;
    private string? _loggedReadFailure;

    public DeviceStore(string path, Action<string> log)
    {
        _path = path;
        _log = log;
    }

    /// <summary>Reads the save's marks into the run when the save changed. Free when it did not.</summary>
    public void Follow(string? save, PowerRun run)
    {
        if (_hasFollowed && save == _save) return;

        _hasFollowed = true;
        _save = save;
        run.Book = new DeviceBook(DeviceMarks.None);

        if (save == null)
        {
            _state = State.NoSave;

            return;
        }

        var text = Read();

        if (text == null)
        {
            _state = State.Unreadable;

            return;
        }

        var marks = DeviceFile.Read(text, save);

        run.Book = new DeviceBook(marks);
        _state = State.Ready;
        _log($"device priorities for save {save}: {marks.LowPriority.Count} low priority, {marks.Shed.Count} switched off to save the battery");
    }

    /// <summary>Why a device cannot be marked now, or null when it can.</summary>
    public Phrase? Refusal() => _state switch
    {
        State.NoSave => Texts.NoSave,
        State.Unreadable => Texts.Unreadable,
        _ => null,
    };

    /// <summary>Writes the book when it changed. False only when a write was tried and failed.</summary>
    public bool SaveIfChanged(PowerRun run)
    {
        if (!run.Book.Changed) return true;

        run.Book.Changed = false;

        if (_state != State.Ready || _save == null) return true;

        var text = Read();

        return text != null && Replace(DeviceFile.Write(text, _save, run.Book.Marks));
    }

    private string? Read()
    {
        try
        {
            var text = File.Exists(_path) ? File.ReadAllText(_path) : "";
            _loggedReadFailure = null;

            return text;
        }
        catch (Exception ex)
        {
            if (ex.Message != _loggedReadFailure) _log($"could not read the device priorities from {_path}: {ex.Message}");

            _loggedReadFailure = ex.Message;

            return null;
        }
    }

    private bool Replace(string text)
    {
        var temp = _path + ".tmp";

        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, _path, overwrite: true);

            return true;
        }
        catch (Exception ex)
        {
            _log($"could not save the device priorities to {_path}: {ex.Message}");

            return false;
        }
    }
}
