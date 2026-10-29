using System;

namespace PowerManagerMod.Core;

/// <summary>How a configured number was taken.</summary>
public enum SpeedVerdict
{
    AsWritten,
    Clamped,
    Rejected,
}

/// <summary>
/// How fast generators burn fuel, checked so it is never zero, negative or out of range. Only <see cref="From"/> makes
/// one, which is what lets the scaling below divide by it without a guard.
/// </summary>
public sealed class Speed
{
    public const float Slowest = 0.25f;
    public const float Fastest = 100000f;

    public static readonly Speed GamePace = new(1f, 1f, SpeedVerdict.AsWritten);

    private Speed(float value, float requested, SpeedVerdict verdict)
    {
        Value = value;
        Requested = requested;
        Verdict = verdict;
    }

    public float Value { get; }

    public float Requested { get; }

    public SpeedVerdict Verdict { get; }

    public bool IsGamePace => Value == 1f;

    /// <summary>Zero, negatives and non-numbers fall back to the game's pace; anything else is pulled into range.</summary>
    public static Speed From(float requested)
    {
        if (!float.IsFinite(requested) || requested <= 0f) return new Speed(1f, requested, SpeedVerdict.Rejected);

        var value = Math.Clamp(requested, Slowest, Fastest);

        return new Speed(value, requested, value == requested ? SpeedVerdict.AsWritten : SpeedVerdict.Clamped);
    }

    /// <summary>One line for the log when a setting was not taken as written; null when it was.</summary>
    public string? Complaint(string key) => Verdict switch
    {
        SpeedVerdict.AsWritten => null,
        SpeedVerdict.Clamped => $"{key} = {Requested} is outside {Slowest} to {Fastest}, using {Value}",
        SpeedVerdict.Rejected => $"{key} = {Requested} is not a speed above 0, using the game's pace (1)",
        _ => throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "unknown verdict"),
    };
}

/// <summary>
/// How much power generators make compared with the game: 1 is the game's amount, 2 is twice as much. Only
/// <see cref="From"/> makes one, so it is never negative, never a non-number and never out of its range.
/// </summary>
public sealed class Multiplier
{
    public static readonly Multiplier GameAmount = new(1f, 1f, SpeedVerdict.AsWritten, 1f, 1f);

    private Multiplier(float value, float requested, SpeedVerdict verdict, float least, float most)
    {
        Value = value;
        Requested = requested;
        Verdict = verdict;
        Least = least;
        Most = most;
    }

    public float Value { get; }

    public float Requested { get; }

    public SpeedVerdict Verdict { get; }

    public float Least { get; }

    public float Most { get; }

    public bool IsGameAmount => Value == 1f;

    /// <summary>
    /// Negatives and non-numbers fall back to the game's amount, and so does 0 when the range starts above it; any
    /// other value is pulled into the range.
    /// </summary>
    public static Multiplier From(float requested, float least, float most)
    {
        var isUnusable = !float.IsFinite(requested) || requested < 0f || requested == 0f && least > 0f;

        if (isUnusable) return new Multiplier(1f, requested, SpeedVerdict.Rejected, least, most);

        var value = Math.Clamp(requested, least, most);

        return new Multiplier(value, requested, value == requested ? SpeedVerdict.AsWritten : SpeedVerdict.Clamped, least, most);
    }

    /// <summary>One line for the log when a setting was not taken as written; null when it was.</summary>
    public string? Complaint(string key) => Verdict switch
    {
        SpeedVerdict.AsWritten => null,
        SpeedVerdict.Clamped => $"{key} = {Requested} is outside {Least} to {Most}, using {Value}",
        SpeedVerdict.Rejected => $"{key} = {Requested} is not a usable amount, using the game's (1)",
        _ => throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "unknown verdict"),
    };
}

/// <summary>The game's values with a speed or an amount applied. A value the game does not use (0 or less) is left alone.</summary>
public static class Scaling
{
    /// <summary>A time left, in any unit: a faster burn leaves less.</summary>
    public static float Duration(float gameValue, Speed speed) =>
        speed.IsGamePace || !float.IsFinite(gameValue) || gameValue <= 0f ? gameValue : gameValue / speed.Value;

    /// <summary>Fuel a generator takes for one stretch of running: a faster burn takes more, so fuel runs out sooner.</summary>
    public static float Burn(float gameBurn, Speed speed) =>
        speed.IsGamePace || !float.IsFinite(gameBurn) || gameBurn <= 0f ? gameBurn : gameBurn * speed.Value;

    /// <summary>A rate such as power output, taken times the multiplier.</summary>
    public static float Rate(float gameRate, Multiplier multiplier) =>
        multiplier.IsGameAmount || !float.IsFinite(gameRate) || gameRate <= 0f ? gameRate : gameRate * multiplier.Value;
}

/// <summary>A table value this mod wrote over: the game's own value and what was written in its place.</summary>
public readonly record struct TableValue(float Game, float Written)
{
    /// <summary>
    /// The value to write now. A value that is not the one last written is the game's (a new row, or a table the game
    /// loaded again), so it becomes the new original; the multiplier is always applied to the original, never twice.
    /// </summary>
    public static TableValue Next(float current, TableValue? last, Multiplier multiplier)
    {
        var game = last is { } written && current == written.Written ? written.Game : current;

        return new TableValue(game, Scaling.Rate(game, multiplier));
    }
}
