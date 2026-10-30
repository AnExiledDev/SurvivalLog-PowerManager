using System;
using System.Collections.Generic;
using UnityEngine;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The Unity object that hands frames to the battery and generator table and to the session. Each distinct failure is logged once, never rethrown into the
/// game; a failure that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class PowerTicker : MonoBehaviour
{
    private const int MaxReportedFailures = 20;

    internal static PanelSession? Session;
    internal static RuleTables? Tables;
    internal static Action<string> Log = _ => { };

    /// <summary>A toast held back until the game has had time to draw its menu, so it is not lost behind the loading screen.</summary>
    internal static Action? Notice;
    private const float NoticeAfterSeconds = 15f;
    private static readonly HashSet<string> ReportedFailures = new();

    public PowerTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            if (Notice != null && Clock.Now() >= NoticeAfterSeconds)
            {
                var notice = Notice;

                Notice = null;
                notice();
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }

        try
        {
            Tables?.Tick(Clock.Now());
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }

        try
        {
            Session?.Update();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void ReportFailure(Exception ex)
    {
        if (ReportedFailures.Count >= MaxReportedFailures || !ReportedFailures.Add(ex.GetType().FullName + ": " + ex.Message)) return;

        Log("update failed (each distinct failure reported once): " + ex);

        if (ReportedFailures.Count == MaxReportedFailures) Log($"{MaxReportedFailures} distinct update failures reported; further ones are not logged");
    }
}
