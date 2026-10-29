using System;
using Il2CppInterop.Runtime;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Unity's frame number, for the hooks that run dozens of times a frame. Time.frameCount through interop boxes its
/// answer on every read, so the engine's own function is called directly, the way the game's code calls it. If the
/// engine does not have it under that name, Time.frameCount answers instead.
/// </summary>
internal static unsafe class UnityFrame
{
    private static readonly delegate* unmanaged<int> FrameCount;

    /// <summary>Explicit, so the lookup runs at the first read in a game frame, never when a worker compiles a caller.</summary>
    static UnityFrame()
    {
        try
        {
            FrameCount = (delegate* unmanaged<int>)(void*)IL2CPP.il2cpp_resolve_icall("UnityEngine.Time::get_frameCount()");
        }
        catch (Exception)
        {
            FrameCount = null;
        }
    }

    public static int Count() => FrameCount != null ? FrameCount() : UnityEngine.Time.frameCount;
}
