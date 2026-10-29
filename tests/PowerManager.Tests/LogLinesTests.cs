using System;
using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>The log file is written on a worker from these lines, so they must come out whole, in order and only once.</summary>
public class LogLinesTests
{
    private const string StampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    [Fact]
    public void Lines_come_out_oldest_first_each_stamped_with_its_own_local_time()
    {
        var lines = new LogLines();
        var first = new DateTime(2026, 10, 26, 8, 30, 1, 250, DateTimeKind.Utc);
        var second = first.AddSeconds(2);

        lines.Add(first, "power: Tesla coil (1) turned on, wave");
        lines.Add(second, "power: Tesla coil (2) turned on, wave");

        var expected =
            first.ToLocalTime().ToString(StampFormat) + " power: Tesla coil (1) turned on, wave" + Environment.NewLine +
            second.ToLocalTime().ToString(StampFormat) + " power: Tesla coil (2) turned on, wave" + Environment.NewLine;

        Assert.Equal(expected, lines.Take());
    }

    [Fact]
    public void Nothing_waiting_gives_nothing_to_write()
    {
        Assert.Null(new LogLines().Take());
    }

    [Fact]
    public void Taken_lines_are_not_written_twice()
    {
        var lines = new LogLines();

        lines.Add(DateTime.UtcNow, "one");
        lines.Take();

        Assert.Null(lines.Take());

        lines.Add(DateTime.UtcNow, "two");

        Assert.EndsWith(" two" + Environment.NewLine, lines.Take());
    }
}
