using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>
/// A home that behaves as the game's handlers do: a toggle flips a generator, a fuel item fills one slot and leaves
/// storage, a full generator takes nothing, and a device switch holds unless the game refuses it.
/// </summary>
internal sealed class FakePowerGame : IPowerGame
{
    public Battery Battery { get; set; } = new(500f, 1000f, 0f, 20f, IsOutage: true, IsLocked: false);

    public List<Generator> Generators { get; } = new();

    public List<Device> Devices { get; } = new();

    public List<FuelStack> Storage { get; } = new();

    /// <summary>Stacks the game will not take.</summary>
    public HashSet<long> Unburnable { get; } = new();

    /// <summary>Devices whose switch the game keeps as it was.</summary>
    public HashSet<long> Stuck { get; } = new();

    /// <summary>Every call that changed the game, in order.</summary>
    public List<string> Calls { get; } = new();

    public PowerScene Read() => new(Battery, Generators.ToList(), Devices.ToList(), new GameLoad(1, 1));

    PowerScene? IPowerGame.Read() => Read();

    public IReadOnlyList<FuelStack> Fuel() => Storage.ToList();

    public string ItemName(int configId) => "item " + configId;

    public bool Toggle(long generatorId)
    {
        var index = Generators.FindIndex(generator => generator.Id == generatorId);

        if (index < 0) return false;

        Calls.Add("toggle " + generatorId);
        Generators[index] = Generators[index] with { IsOn = !Generators[index].IsOn };

        return true;
    }

    public bool SetAutoStart(long generatorId, bool isOn, float at)
    {
        var index = Generators.FindIndex(generator => generator.Id == generatorId);

        if (index < 0) return false;

        Calls.Add($"auto {generatorId} {isOn} {at:0.0}");
        Generators[index] = Generators[index] with { AutoStart = isOn, AutoStartAt = at };

        return true;
    }

    public FuelAdd AddFuel(long generatorId, long itemId)
    {
        var index = Generators.FindIndex(generator => generator.Id == generatorId);

        if (index < 0) return FuelAdd.Gone;

        var generator = Generators[index];

        if (generator.IsFull) return FuelAdd.Full;

        var stackIndex = Storage.FindIndex(stack => stack.ItemId == itemId);

        if (stackIndex < 0 || Unburnable.Contains(itemId)) return FuelAdd.Refused;

        var stack = Storage[stackIndex];

        Calls.Add($"fuel {generatorId} {stack.ConfigId}");

        if (stack.Count == 1) Storage.RemoveAt(stackIndex);
        else Storage[stackIndex] = stack with { Count = stack.Count - 1 };

        Generators[index] = generator with { FuelSlots = generator.FuelSlots + 1, FuelSeconds = generator.FuelSeconds + 600f };

        return FuelAdd.Added;
    }

    public bool? Switch(long deviceId, bool isOn)
    {
        var index = Devices.FindIndex(device => device.Id == deviceId);

        if (index < 0) return null;

        Calls.Add($"switch {deviceId} {isOn}");

        if (Stuck.Contains(deviceId)) return Devices[index].IsOn;

        Devices[index] = Devices[index] with { IsOn = isOn };

        return isOn;
    }

    /// <summary>Runs the queue as the plugin does, one step per frame on a fresh read, and returns what it said.</summary>
    public List<string> RunAll(PowerRun run, float now = 0f)
    {
        var said = new List<string>();

        for (var frame = 0; run.HasWork && frame < 200; frame++)
        {
            if (run.Step(Read(), now, isChinese: false) is { } outcome) said.Add(outcome.Text);
        }

        Assert.False(run.HasWork, "the queue never emptied");

        return said;
    }
}
