using System;
using GameCore.HotUpdate;

namespace PowerManagerMod.Adapters;

/// <summary>
/// Which save is being played. The game writes each save to its own file named by GameSaveData.FileName, so that
/// name is the save's identity for the power settings.
/// </summary>
internal static class SaveSlot
{
    /// <summary>The loaded save's file name, or null before a save is loaded or when it cannot be read.</summary>
    public static string? CurrentKey()
    {
        try
        {
            var name = GameSingleton.Existing<GameSaveManager>()?.gameSave?.FileName;

            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
