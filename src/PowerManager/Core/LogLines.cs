using System;
using System.Collections.Concurrent;
using System.Text;

namespace PowerManagerMod.Core;

/// <summary>
/// Log lines waiting for the file. Adding one only stores the message and the time, so the main thread never waits
/// on the disk; the date stamp and the text for the file are built by whoever takes them.
/// </summary>
public sealed class LogLines
{
    private const string StampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly ConcurrentQueue<(DateTime AtUtc, string Message)> _waiting = new();

    public void Add(DateTime atUtc, string message) => _waiting.Enqueue((atUtc, message));

    /// <returns>Every line waiting, oldest first, each stamped with its local time and ending in a line break; null when none are waiting.</returns>
    public string? Take()
    {
        if (_waiting.IsEmpty) return null;

        var text = new StringBuilder();

        while (_waiting.TryDequeue(out var line))
        {
            text.Append(line.AtUtc.ToLocalTime().ToString(StampFormat)).Append(' ').Append(line.Message).Append(Environment.NewLine);
        }

        return text.Length == 0 ? null : text.ToString();
    }
}
