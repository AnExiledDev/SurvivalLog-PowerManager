using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.StrangerTrade;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The game's side of the panel. Reads go through the game's power manager and the home's electrical furniture (the
/// same list the game's own power panel shows); every change is one of the game's own button handlers. Each call fails
/// soft: an exception is logged once per kind of call and answered as nothing read or nothing done, and one device that
/// cannot be read is left out while the rest are not.
/// </summary>
internal sealed class GamePower : IPowerGame
{
    /// <summary>Outdoor defenses sit in this furniture slot (Defense Manager's DefenseSlot).</summary>
    private const int DefenseSlot = 10;

    private const int ConsumerType = 1;
    private const int GeneratorType = 3;

    private readonly Action<string> _log;
    private readonly bool _isDefenseManagerLoaded;
    private readonly HashSet<string> _reportedFailures = new();

    public GamePower(Action<string> log, bool isDefenseManagerLoaded)
    {
        _log = log;
        _isDefenseManagerLoaded = isDefenseManagerLoaded;
    }

    /// <summary>Whether a game is loaded and past the prologue, so the HUD button and the panel have something to show.</summary>
    public static bool IsLive()
    {
        try
        {
            return LoadedGame() != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public PowerScene? Read()
    {
        try
        {
            return ReadScene();
        }
        catch (Exception ex)
        {
            ReportOnce("power read", ex);

            return null;
        }
    }

    public IReadOnlyList<FuelStack> Fuel()
    {
        var stock = new List<FuelStack>();

        try
        {
            var game = LoadedGame();

            if (game == null) return stock;

            var (world, config, agents) = game.Value;
            var items = world._ItemManager;
            var rows = config._Config_Item_Dict;

            if (items == null || rows == null) return stock;

            var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);

            for (var i = 0; found != null && i < found.Count; i++)
            {
                var storage = found[i];

                if (storage == null || IsSetApart(storage)) continue;

                var held = items.GetItemDataList(storage._InstanceId_k__BackingField);

                for (var j = 0; held != null && j < held.Count; j++)
                {
                    var item = held[j];

                    if (item == null || item.ItemCount <= 0) continue;

                    var configId = item.ItemConfigId;

                    if (IsFuel(rows, configId)) stock.Add(new FuelStack(item._InstanceId_k__BackingField, configId, item.ItemCount));
                }
            }
        }
        catch (Exception ex)
        {
            ReportOnce("fuel read", ex);
        }

        return stock;
    }

    public string ItemName(int configId)
    {
        try
        {
            var config = GameSingleton.Existing<ConfigManager>();
            var rows = config?._Config_Item_Dict;

            if (config != null && rows != null && rows.TryGetValue(configId, out var row) && row != null)
            {
                return Translate(config, row._ItemName_k__BackingField) ?? NonEmpty(row._ItemName_Local_k__BackingField) ?? NonEmpty(row._ItemName_k__BackingField) ?? $"#{configId}";
            }
        }
        catch (Exception ex)
        {
            ReportOnce("item name", ex);
        }

        return $"#{configId}";
    }

    public bool Toggle(long generatorId)
    {
        try
        {
            var generator = FurnitureOf(generatorId);

            if (generator == null) return false;

            generator.OnFuelGeneratorToggle();

            return true;
        }
        catch (Exception ex)
        {
            ReportOnce("generator toggle", ex);

            return false;
        }
    }

    public bool SetAutoStart(long generatorId, bool isOn, float at)
    {
        try
        {
            var generator = FurnitureOf(generatorId);

            if (generator == null) return false;

            generator.OnFuelGeneratorSetAutoStart(isOn, at);

            return true;
        }
        catch (Exception ex)
        {
            ReportOnce("generator auto-start", ex);

            return false;
        }
    }

    /// <summary>
    /// The game's handler returns nothing, so what it did is read off the generator: the fuel it holds grows when the
    /// item went in. A generator already full is answered before the call, so nothing is asked of the game.
    /// </summary>
    public FuelAdd AddFuel(long generatorId, long itemId)
    {
        try
        {
            var generator = FurnitureOf(generatorId);
            var fuel = generator == null ? null : AgentTools.GetAgentComponent<GeneratorFuelComponent>(generator);

            if (generator == null || fuel == null) return FuelAdd.Gone;
            if (fuel.IsFull()) return FuelAdd.Full;

            var before = fuel.GetTotalRemainingBurnValue();

            generator.OnFuelGeneratorFuelAdd(itemId);

            return fuel.GetTotalRemainingBurnValue() > before ? FuelAdd.Added : FuelAdd.Refused;
        }
        catch (Exception ex)
        {
            ReportOnce("generator refuel", ex);

            return FuelAdd.Gone;
        }
    }

    /// <summary>The game ignores the call while its power switches are locked, so the switch is read back rather than assumed.</summary>
    public bool? Switch(long deviceId, bool isOn)
    {
        try
        {
            var power = GameSingleton.Existing<BattleLogicWorld>()?._PowerManager;

            if (power == null) return null;

            power.SetConsumerSwitch(deviceId, isOn);

            return power.IsConsumerOn(deviceId);
        }
        catch (Exception ex)
        {
            ReportOnce("device switch", ex);

            return null;
        }
    }

    private PowerScene? ReadScene()
    {
        var game = LoadedGame();

        if (game == null) return null;

        var (world, config, _) = game.Value;
        var power = world._PowerManager;

        if (power == null) return null;

        var battery = new Battery(
            power._CurrentStoredPower_k__BackingField,
            power._TotalCapacity_k__BackingField,
            power._TotalGeneration_k__BackingField,
            power._TotalConsumption_k__BackingField,
            power.IsInPowerOutage(),
            power._IsLocked_k__BackingField);
        var generators = new List<Generator>();
        var devices = new List<Device>();
        var found = power.GetHomeElectricalFurnituresSnapshot();

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture == null || furniture._IsDead_k__BackingField) continue;

            try
            {
                ReadOne(furniture, config, power, generators, devices);
            }
            catch (Exception ex)
            {
                ReportOnce("power furniture read", ex);
            }
        }

        return new PowerScene(battery, generators, devices, ReadLoad(world));
    }

    private void ReadOne(Furniture furniture, ConfigManager config, PowerManager power, List<Generator> generators, List<Device> devices)
    {
        var configId = furniture._AgentConfigId_k__BackingField;
        var rows = config._Config_Furniture_Dict;

        if (rows == null || !rows.TryGetValue(configId, out var row) || row == null) return;

        var electrical = ElectricalRow(config, row);
        var id = furniture._InstanceId_k__BackingField;

        if (electrical?._ElectricalType_k__BackingField == GeneratorType)
        {
            var fuel = AgentTools.GetAgentComponent<GeneratorFuelComponent>(furniture);

            if (fuel == null || fuel._IsFuelHeater_k__BackingField) return;

            var slots = fuel._FuelSlots_k__BackingField;

            generators.Add(new Generator(
                id,
                NameOf(config, row, configId),
                power.GetGeneratorState(id),
                slots?.Count ?? 0,
                fuel.MaxFuelSlots,
                fuel.GetRemainingGameSeconds(),
                fuel._AutoStartEnabled_k__BackingField,
                fuel._AutoStartThreshold_k__BackingField));

            return;
        }

        if (row._ElectricalType_k__BackingField != ConsumerType && electrical?._ElectricalType_k__BackingField != ConsumerType) return;

        var transform = AgentTools.GetAgentComponent<TransformComponent>(furniture);
        var position = transform?.Position;
        var key = DeviceKey.Of(configId, position?.x ?? 0f, position?.y ?? 0f, position?.z ?? 0f);
        var isManaged = _isDefenseManagerLoaded && row._SlotType_k__BackingField == DefenseSlot;

        devices.Add(new Device(id, key, NameOf(config, row, configId), power.IsConsumerOn(id, row._PowerMode_k__BackingField), PowerManager.GetEffectivePowerCost(row, id), isManaged));
    }

    private static Config_FurnitureElectrical? ElectricalRow(ConfigManager config, Config_Furniture row)
    {
        var rows = config._Config_FurnitureElectrical_Dict;

        return rows != null && rows.TryGetValue(row._ElectricalFurnitureID_k__BackingField, out var electrical) ? electrical : null;
    }

    /// <summary>
    /// Burnable and not a task item: the game's own test in the generator's fuel handler, so nothing offered here is
    /// refused there.
    /// </summary>
    private static bool IsFuel(Il2CppSystem.Collections.Generic.Dictionary<int, Config_Item> rows, int configId) =>
        rows.TryGetValue(configId, out var row) && row != null && row._Burnable_k__BackingField && !GiveAwayRules.IsTaskItem(configId);

    /// <summary>
    /// Storage the game keeps out of the home's shared pool, the neighbor's basket (what is there is going to her), and
    /// stoves (their pot food belongs to the dish).
    /// </summary>
    private static bool IsSetApart(Furniture storage)
    {
        if (ItemManager.IsExcludedFromCostPool(storage._InstanceId_k__BackingField)) return true;
        if (AgentTools.GetAgentComponent<CookingComponent>(storage) != null) return true;

        var basketId = NeighborRules.GetBasketFurnitureId();

        return basketId > 0 && storage._AgentConfigId_k__BackingField == basketId;
    }

    /// <summary>Null on the title screen, while a save loads and in the prologue.</summary>
    private static (BattleLogicWorld World, ConfigManager Config, AgentManager Agents)? LoadedGame()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var agents = world?._AgentManager;

        if (world == null || config == null || agents == null || world._GameTimeManager?.WorldStateType != WorldStateType.PostDisaster) return null;

        return (world, config, agents);
    }

    private static Furniture? FurnitureOf(long id) => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetAgent(id)?.TryCast<Furniture>();

    /// <summary>
    /// The load, by the addresses of the game objects behind it, so a reload is told apart from the same game going on.
    /// </summary>
    private static GameLoad ReadLoad(BattleLogicWorld world)
    {
        var save = GameSingleton.Existing<GameSaveManager>()?.gameSave;

        return new GameLoad(world.Pointer.ToInt64(), save?.Pointer.ToInt64() ?? 0);
    }

    private static string NameOf(ConfigManager config, Config_Furniture row, int configId) =>
        Translate(config, row._Name_k__BackingField) ?? NonEmpty(row._Name_Local_k__BackingField) ?? NonEmpty(row._Name_k__BackingField) ?? $"#{configId}";

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private void ReportOnce(string operation, Exception ex)
    {
        if (_reportedFailures.Add(operation)) _log($"{operation} failed (each kind reported once): {ex}");
    }
}
