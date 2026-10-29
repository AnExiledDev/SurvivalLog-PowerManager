using System.Diagnostics;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Seconds since the mod first asked, for its timers. Unity's own clock (Time.unscaledTime) is a call into the game
/// that boxes its answer every frame; a Stopwatch read allocates nothing.
/// </summary>
internal static class Clock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    public static float Now() => (float)Watch.Elapsed.TotalSeconds;
}
