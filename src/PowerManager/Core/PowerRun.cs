using System;
using System.Collections.Generic;

namespace PowerManagerMod.Core;

/// <summary>A result line for the player. IsError shows it as an error toast while the panel is closed.</summary>
public sealed record Outcome(string Text, bool IsError = false);

/// <summary>
/// The devices marked low priority and the ones the mod switched off to save the battery, for the save that is loaded.
/// Changed is set whenever either list changes, so the caller knows to write the file.
/// </summary>
public sealed class DeviceBook
{
    public DeviceBook(DeviceMarks marks)
    {
        Low = new HashSet<DeviceKey>(marks.LowPriority);
        Shed = new HashSet<DeviceKey>(marks.Shed);
    }

    public HashSet<DeviceKey> Low { get; }

    public HashSet<DeviceKey> Shed { get; }

    public bool Changed { get; set; }

    public DeviceMarks Marks => new(new List<DeviceKey>(Low), new List<DeviceKey>(Shed));
}

/// <summary>
/// The work the panel and the automatic passes ask for, done one game call per frame. Start, stop and auto-start each
/// take one call; refuelling puts in one item per frame until the generator is full or the fuel list has nothing left
/// in storage. A batch ("start all") reports once when it is queued and its steps go quietly.
/// </summary>
public sealed class PowerRun
{
    /// <summary>A generator auto-refuel found no fuel for is tried again after this long, without a second message.</summary>
    public const float StarvedRetrySeconds = 60f;

    private readonly IPowerGame _game;
    private readonly LinkedList<Job> _jobs = new();
    private readonly Dictionary<long, float> _starvedUntil = new();
    private readonly HashSet<long> _starvedReported = new();
    private readonly HashSet<long> _refuelling = new();

    public PowerRun(IPowerGame game)
    {
        _game = game;
    }

    public FuelOrder Order { get; set; } = FuelOrder.Parse(FuelOrder.Default);

    public DeviceBook Book { get; set; } = new(DeviceMarks.None);

    public bool HasWork => _jobs.Count > 0;

    public bool IsRefuelling(long generatorId) => _refuelling.Contains(generatorId);

    /// <summary>A new game was loaded: work queued for the old one is dropped.</summary>
    public void Clear()
    {
        _jobs.Clear();
        _refuelling.Clear();
        _starvedUntil.Clear();
        _starvedReported.Clear();
    }

    public void Start(long id) => _jobs.AddLast(new Job.Start(id, IsQuiet: false));

    public void Stop(long id) => _jobs.AddLast(new Job.Stop(id, IsQuiet: false));

    public void AutoStart(long id, bool isOn, float? at = null) => _jobs.AddLast(new Job.Auto(id, isOn, at, IsQuiet: false));

    public void Refuel(long id) => QueueRefuel(id, isAuto: false);

    public void Switch(long id, bool isOn) => _jobs.AddLast(new Job.Switch(id, isOn, SwitchRole.Player));

    /// <summary>Marks a device low priority, or back to normal. Takes effect at once, with no game call.</summary>
    public Outcome MarkLow(PowerScene scene, long id, bool isLow, bool isChinese)
    {
        if (Find(scene.Devices, id) is not { } device) return new Outcome(Texts.DeviceGone.Pick(isChinese));
        if (device.IsManaged) return new Outcome(Texts.Managed.Say(isChinese, device.Name));

        var changed = isLow ? Book.Low.Add(device.Key) : Book.Low.Remove(device.Key);

        if (changed) Book.Changed = true;

        return new Outcome((isLow ? Texts.MarkedLow : Texts.MarkedNormal).Say(isChinese, device.Name));
    }

    public Outcome StartAll(PowerScene scene, bool isChinese)
    {
        if (scene.Generators.Count == 0) return new Outcome(Texts.NoGenerators.Pick(isChinese));

        var count = 0;

        foreach (var generator in scene.Generators)
        {
            if (generator.IsOn || !generator.HasFuel) continue;

            _jobs.AddLast(new Job.Start(generator.Id, IsQuiet: true));
            count++;
        }

        return new Outcome(count == 0 ? Texts.NothingToStart.Pick(isChinese) : Texts.StartingAll.Say(isChinese, count));
    }

    public Outcome StopAll(PowerScene scene, bool isChinese)
    {
        if (scene.Generators.Count == 0) return new Outcome(Texts.NoGenerators.Pick(isChinese));

        var count = 0;

        foreach (var generator in scene.Generators)
        {
            if (!generator.IsOn) continue;

            _jobs.AddLast(new Job.Stop(generator.Id, IsQuiet: true));
            count++;
        }

        return new Outcome(count == 0 ? Texts.NothingToStop.Pick(isChinese) : Texts.StoppingAll.Say(isChinese, count));
    }

    public Outcome AutoStartAll(PowerScene scene, bool isOn, bool isChinese)
    {
        if (scene.Generators.Count == 0) return new Outcome(Texts.NoGenerators.Pick(isChinese));

        foreach (var generator in scene.Generators) _jobs.AddLast(new Job.Auto(generator.Id, isOn, null, IsQuiet: true));

        return new Outcome((isOn ? Texts.AutoAllOn : Texts.AutoAllOff).Say(isChinese, scene.Generators.Count));
    }

    /// <summary>Every generator that is not full is filled, one after another, each reporting what went in.</summary>
    public Outcome? RefuelAll(PowerScene scene, bool isChinese)
    {
        if (scene.Generators.Count == 0) return new Outcome(Texts.NoGenerators.Pick(isChinese));

        var count = 0;

        foreach (var generator in scene.Generators)
        {
            if (generator.IsFull) continue;

            QueueRefuel(generator.Id, isAuto: false);
            count++;
        }

        return count == 0 ? new Outcome(Texts.AllFull.Pick(isChinese)) : null;
    }

    /// <summary>
    /// The automatic work, run every few seconds while nothing else is queued: load shedding first, then auto-refuel.
    /// Returns the shedding line when devices are switched; refuels report as they finish.
    /// </summary>
    public Outcome? AutoPass(PowerScene scene, int shedBelow, int autoRefuelMinutes, float now, bool isChinese)
    {
        if (HasWork) return null;

        var outcome = Shed(scene, shedBelow, isChinese);

        if (autoRefuelMinutes <= 0) return outcome;

        foreach (var generator in scene.Generators)
        {
            if (!AutoRefuel.Wants(generator, autoRefuelMinutes)) continue;
            if (_starvedUntil.TryGetValue(generator.Id, out var until) && now < until) continue;

            QueueRefuel(generator.Id, isAuto: true);
        }

        return outcome;
    }

    /// <summary>Does the next queued job: at most one call that changes the game. Null when it has nothing to say.</summary>
    public Outcome? Step(PowerScene scene, float now, bool isChinese)
    {
        var first = _jobs.First;

        if (first == null) return null;

        _jobs.RemoveFirst();

        return first.Value switch
        {
            Job.Start start => DoStart(scene, start, isChinese),
            Job.Stop stop => DoStop(scene, stop, isChinese),
            Job.Auto auto => DoAuto(scene, auto, isChinese),
            Job.Refuel refuel => DoRefuel(scene, refuel, now, isChinese),
            Job.Switch press => DoSwitch(scene, press, isChinese),
            _ => throw new InvalidOperationException("unknown job " + first.Value),
        };
    }

    private Outcome? Shed(PowerScene scene, int shedBelow, bool isChinese)
    {
        var plan = Shedding.Plan(scene.Battery, scene.Devices, Book.Low, Book.Shed, shedBelow);

        foreach (var device in plan.SwitchOff) _jobs.AddLast(new Job.Switch(device.Id, false, SwitchRole.Shed));
        foreach (var device in plan.SwitchOn) _jobs.AddLast(new Job.Switch(device.Id, true, SwitchRole.Restore));

        if (plan.SwitchOff.Count > 0) return new Outcome(Texts.Shedding.Say(isChinese, plan.SwitchOff.Count));
        if (plan.SwitchOn.Count > 0) return new Outcome(Texts.Restoring.Say(isChinese, plan.SwitchOn.Count));

        ForgetSettled(scene, shedBelow);

        return null;
    }

    /// <summary>
    /// Once the battery is back over the restore level, a device the mod switched off but that is on again (the player
    /// switched it) or no longer there is no longer the mod's to give back.
    /// </summary>
    private void ForgetSettled(PowerScene scene, int shedBelow)
    {
        var battery = scene.Battery;
        var isRunning = shedBelow > 0 && battery.IsOutage && battery.Capacity > 0f;

        if (Book.Shed.Count == 0 || battery.IsLocked || isRunning && battery.Percent < shedBelow + Shedding.RestoreMargin) return;

        var stillOff = new HashSet<DeviceKey>();

        foreach (var device in scene.Devices)
        {
            if (!device.IsOn && Book.Shed.Contains(device.Key)) stillOff.Add(device.Key);
        }

        if (stillOff.Count == Book.Shed.Count) return;

        Book.Shed.IntersectWith(stillOff);
        Book.Changed = true;
    }

    private void QueueRefuel(long id, bool isAuto)
    {
        if (!_refuelling.Add(id)) return;

        _jobs.AddLast(new Job.Refuel(id, isAuto));
    }

    private Outcome? DoStart(PowerScene scene, Job.Start job, bool isChinese)
    {
        if (Find(scene.Generators, job.Id) is not { } generator) return Say(job.IsQuiet, Texts.GeneratorGone.Pick(isChinese));
        if (generator.IsOn) return null;
        if (!generator.HasFuel) return Say(job.IsQuiet, Texts.NoFuel.Say(isChinese, generator.Name));
        if (scene.Battery.IsLocked) return Say(job.IsQuiet, Texts.Locked.Pick(isChinese));
        if (!_game.Toggle(generator.Id)) return new Outcome(Texts.Failed.Pick(isChinese), IsError: true);

        return Say(job.IsQuiet, Texts.Started.Say(isChinese, generator.Name));
    }

    private Outcome? DoStop(PowerScene scene, Job.Stop job, bool isChinese)
    {
        if (Find(scene.Generators, job.Id) is not { } generator) return Say(job.IsQuiet, Texts.GeneratorGone.Pick(isChinese));
        if (!generator.IsOn) return null;
        if (scene.Battery.IsLocked) return Say(job.IsQuiet, Texts.Locked.Pick(isChinese));
        if (!_game.Toggle(generator.Id)) return new Outcome(Texts.Failed.Pick(isChinese), IsError: true);

        return Say(job.IsQuiet, (generator.AutoStart ? Texts.StoppedAutoOff : Texts.Stopped).Say(isChinese, generator.Name));
    }

    private Outcome? DoAuto(PowerScene scene, Job.Auto job, bool isChinese)
    {
        if (Find(scene.Generators, job.Id) is not { } generator) return Say(job.IsQuiet, Texts.GeneratorGone.Pick(isChinese));

        var at = job.At ?? generator.AutoStartAt;

        if (generator.AutoStart == job.IsOn && generator.AutoStartAt == at) return null;
        if (!_game.SetAutoStart(generator.Id, job.IsOn, at)) return new Outcome(Texts.Failed.Pick(isChinese), IsError: true);

        var text = job.IsOn ? Texts.AutoOn.Say(isChinese, generator.Name, (int)Math.Round(at * 100f)) : Texts.AutoOff.Say(isChinese, generator.Name);

        return Say(job.IsQuiet, text);
    }

    private Outcome? DoRefuel(PowerScene scene, Job.Refuel job, float now, bool isChinese)
    {
        var generator = Find(scene.Generators, job.Id);

        if (generator == null) return Finish(job, Texts.GeneratorGone.Pick(isChinese), isChinese, null);
        if (job.Added == 0 && generator.IsFull) return Finish(job, job.IsAuto ? null : Texts.AlreadyFull.Say(isChinese, generator.Name), isChinese, generator.Name);

        if (Order.Next(_game.Fuel(), job.Refused) is not { } stack) return RanOut(job, generator.Name, now, isChinese);

        switch (_game.AddFuel(generator.Id, stack.ItemId))
        {
            case FuelAdd.Added:
                job.Added++;
                _jobs.AddFirst(job);

                return null;
            case FuelAdd.Refused:
                job.Refused.Add(stack.ItemId);
                _jobs.AddFirst(job);

                return null;
            case FuelAdd.Full:
                return Finish(job, job.Added == 0 && !job.IsAuto ? Texts.AlreadyFull.Say(isChinese, generator.Name) : null, isChinese, generator.Name);
            default:
                return Finish(job, Texts.GeneratorGone.Pick(isChinese), isChinese, null);
        }
    }

    /// <summary>The fuel list has nothing left in storage. Auto-refuel says so once per generator until it finds fuel again.</summary>
    private Outcome? RanOut(Job.Refuel job, string name, float now, bool isChinese)
    {
        if (job.Added > 0) return Finish(job, null, isChinese, name, Texts.RefuelledRanOut);

        _refuelling.Remove(job.Id);

        if (!job.IsAuto) return new Outcome(Texts.NoFuelInStorage.Pick(isChinese));

        _starvedUntil[job.Id] = now + StarvedRetrySeconds;

        return _starvedReported.Add(job.Id) ? new Outcome(Texts.AutoNoFuel.Say(isChinese, name)) : null;
    }

    /// <summary>
    /// The refuel is over. What went in is reported when anything did, in <paramref name="added"/>'s words when given,
    /// otherwise <paramref name="otherwise"/>; an automatic refuel that is over without a problem may be warned again
    /// the next time it runs out.
    /// </summary>
    private Outcome? Finish(Job.Refuel job, string? otherwise, bool isChinese, string? name, Counted? added = null)
    {
        _refuelling.Remove(job.Id);

        if (job.Added == 0) return otherwise == null ? null : new Outcome(otherwise);

        _starvedUntil.Remove(job.Id);
        _starvedReported.Remove(job.Id);

        return new Outcome((added ?? (job.IsAuto ? Texts.AutoRefuelled : Texts.Refuelled)).Say(isChinese, job.Added, name ?? ""));
    }

    private Outcome? DoSwitch(PowerScene scene, Job.Switch job, bool isChinese)
    {
        var isPlayer = job.Role == SwitchRole.Player;

        if (Find(scene.Devices, job.Id) is not { } device) return isPlayer ? new Outcome(Texts.DeviceGone.Pick(isChinese)) : null;
        if (device.IsManaged) return isPlayer ? new Outcome(Texts.Managed.Say(isChinese, device.Name)) : null;
        if (device.IsOn == job.IsOn) return Settle(job, device.Key, isChinese, null);
        if (scene.Battery.IsLocked) return isPlayer ? new Outcome(Texts.Locked.Pick(isChinese)) : null;

        var after = _game.Switch(device.Id, job.IsOn);

        if (after == null) return new Outcome(Texts.Failed.Pick(isChinese), IsError: true);
        if (after != job.IsOn) return isPlayer ? new Outcome(Texts.NotSwitched.Say(isChinese, device.Name)) : null;

        return Settle(job, device.Key, isChinese, (job.IsOn ? Texts.SwitchedOn : Texts.SwitchedOff).Say(isChinese, device.Name));
    }

    /// <summary>
    /// A device the mod switched off is noted so it alone comes back on; a device the player switched by hand is the
    /// player's again, so the mod never turns it back on behind them.
    /// </summary>
    private Outcome? Settle(Job.Switch job, DeviceKey key, bool isChinese, string? text)
    {
        var changed = job.Role switch
        {
            SwitchRole.Shed => Book.Shed.Add(key),
            _ => Book.Shed.Remove(key),
        };

        if (changed) Book.Changed = true;

        return job.Role == SwitchRole.Player && text != null ? new Outcome(text) : null;
    }

    private static Outcome? Say(bool isQuiet, string text) => isQuiet ? null : new Outcome(text);

    private static Generator? Find(IReadOnlyList<Generator> generators, long id)
    {
        foreach (var generator in generators)
        {
            if (generator.Id == id) return generator;
        }

        return null;
    }

    private static Device? Find(IReadOnlyList<Device> devices, long id)
    {
        foreach (var device in devices)
        {
            if (device.Id == id) return device;
        }

        return null;
    }

    private enum SwitchRole
    {
        Player,
        Shed,
        Restore,
    }

    private abstract record Job
    {
        public sealed record Start(long Id, bool IsQuiet) : Job;

        public sealed record Stop(long Id, bool IsQuiet) : Job;

        public sealed record Auto(long Id, bool IsOn, float? At, bool IsQuiet) : Job;

        public sealed record Switch(long Id, bool IsOn, SwitchRole Role) : Job;

        /// <summary>Carries what went in so far and the stacks the game would not take, across the frames it runs.</summary>
        public sealed record Refuel(long Id, bool IsAuto) : Job
        {
            public int Added { get; set; }

            public HashSet<long> Refused { get; } = new();
        }
    }
}
