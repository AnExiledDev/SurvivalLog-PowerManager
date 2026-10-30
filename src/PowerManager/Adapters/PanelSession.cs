using System;
using System.Collections.Generic;
using ExiledToolkit;
using PowerManagerMod.Core;
using UnityEngine;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The panel's game side, run once a frame: its open state, the view it sends to page.js, the presses the page asks
/// for, the queued work (one game call a frame) and the automatic pass (load shedding and auto-refuel). Page messages
/// arrive on the web view's thread, so they are queued and handled here, in this frame.
/// </summary>
internal sealed class PanelSession
{
    private const float RefreshSeconds = 1f;
    private const float LiveSeconds = 1f;
    private const float LiveOffset = 0.25f;
    private const float FuelSeconds = 2f;
    private const float AutoSeconds = 5f;
    private const float AutoOffset = 1.4f;

    private static readonly IReadOnlyList<FuelStack> NoStock = Array.Empty<FuelStack>();
    private static readonly IReadOnlyList<FuelItem> NoItems = Array.Empty<FuelItem>();

    private readonly GamePower _game;
    private readonly WebPage _page;
    private readonly PanelConfig _config;
    private readonly DeviceStore _store;
    private readonly PowerRun _run;
    private readonly Action<string> _log;
    private readonly Func<long, bool> _isRefuelling;
    private readonly Func<int, string> _itemName;
    private readonly Func<bool> _isPanelKeyDown;
    private static readonly Func<bool> IsTyping = () => ExiledToolkit.Typing.IsActive;
    private readonly Dictionary<int, string> _names = new();

    private bool? _namesInChinese;
    private string _shownView = "";
    private float _nextRefreshAt;
    private float _nextLiveAt = LiveOffset;
    private float _nextFuelAt;
    private float _nextAutoAt = AutoOffset;
    private bool? _sentLive;
    private bool _isOpen;
    private bool _hasReportedNotSaved;
    private bool _hasReportedAutoFailure;
    private GameLoad? _load;
    private IReadOnlyList<FuelStack> _stock = NoStock;
    private IReadOnlyList<FuelItem> _fuelItems = NoItems;

    // The fuel tab's JSON and what it was built from, so it is rebuilt only when the order, the storage read, the item
    // list or the language changed.
    private string _fuelJson = "";
    private FuelOrder? _fuelJsonOrder;
    private IReadOnlyList<FuelStack>? _fuelJsonStock;
    private IReadOnlyList<FuelItem>? _fuelJsonItems;
    private bool? _fuelJsonInChinese;

    public PanelSession(GamePower game, WebPage page, PanelConfig config, DeviceStore store, Action<string> log)
    {
        _game = game;
        _page = page;
        _config = config;
        _store = store;
        _log = log;
        _run = new PowerRun(game);
        _isRefuelling = _run.IsRefuelling;
        _itemName = NameOfItem;
        _isPanelKeyDown = () => Input.GetKeyDown(_config.PanelKey);
    }

    private static string FailedStatus => Texts.Failed.Pick(GameLanguage.IsChinese());

    public void Update()
    {
        var now = Clock.Now();
        _page.KeepInstalled(now);

        while (PageMessages.TryTake(out var command)) HandleSafely(command);

        if (now >= _nextLiveAt) SendLive(now);

        var panelKey = _config.PanelKey;

        // One ask of Unity in a frame with no key going down, rather than one per key the panel listens for.
        var isAnyKeyDown = (panelKey != KeyCode.None || _isOpen) && Input.anyKeyDown;

        // A key typed into a text box (a search, another mod's field) is the player's text, not the panel key.
        if (isAnyKeyDown && Hotkey.Fires(panelKey != KeyCode.None, _isPanelKeyDown, IsTyping))
        {
            Toggle();

            return;
        }

        if (_isOpen && isAnyKeyDown && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();

            return;
        }

        Work(now);

        if (_isOpen && now >= _nextRefreshAt) PushView(now);
    }

    /// <summary>
    /// One queued job a frame while there are any; otherwise the automatic pass on its own beat, only when there is
    /// something for it to do (auto-refuel or load shedding on, or devices still to give back).
    /// </summary>
    private void Work(float now)
    {
        try
        {
            if (_run.HasWork)
            {
                var scene = Scene();

                if (scene == null) return;

                Report(_run.Step(scene, now, GameLanguage.IsChinese()));
                SaveMarks();

                if (!_run.HasWork) _nextRefreshAt = 0f;

                return;
            }

            if (now < _nextAutoAt) return;

            _nextAutoAt += AutoSeconds;

            if (_nextAutoAt <= now) _nextAutoAt = now + AutoSeconds;

            var minutes = _config.AutoRefuelMinutes;
            var shedBelow = _config.ShedBelow;

            if (minutes <= 0 && shedBelow <= 0 && _run.Book.Shed.Count == 0) return;

            var auto = Scene();

            if (auto == null) return;

            _run.Order = _config.Order;
            Report(_run.AutoPass(auto, shedBelow, minutes, now, GameLanguage.IsChinese()));
            SaveMarks();
        }
        catch (Exception ex)
        {
            if (!_hasReportedAutoFailure) _log($"power work failed (reported once): {ex}");

            _hasReportedAutoFailure = true;
        }
    }

    /// <summary>
    /// The game as it is now, with the run following the loaded game: a new load drops work queued for the old one, and
    /// a new save brings its own device marks. Null with no game loaded.
    /// </summary>
    private PowerScene? Scene()
    {
        var scene = _game.Read();

        if (scene == null)
        {
            if (_run.HasWork) _run.Clear();

            return null;
        }

        if (_load != scene.Load)
        {
            if (_load != null) _log("a game was loaded, queued work dropped");

            _load = scene.Load;
            _run.Clear();
        }

        _store.Follow(SaveSlot.CurrentKey(), _run);

        return scene;
    }

    private void SaveMarks()
    {
        if (_store.SaveIfChanged(_run) || _hasReportedNotSaved) return;

        _hasReportedNotSaved = true;
        Status(new Outcome(Texts.NotSaved.Pick(GameLanguage.IsChinese()), IsError: true));
    }

    private void Report(Outcome? outcome)
    {
        if (outcome == null) return;

        _log(outcome.Text);
        Status(outcome);
    }

    /// <summary>One command that throws must not cost the rest of the frame its commands or the key checks.</summary>
    private void HandleSafely(PageCommand command)
    {
        try
        {
            Handle(command);
        }
        catch (Exception ex)
        {
            _log($"page command {command} failed: {ex}");
            Status(new Outcome(FailedStatus, IsError: true));
        }
    }

    private void Handle(PageCommand command)
    {
        var isChinese = GameLanguage.IsChinese();

        switch (command)
        {
            case PageCommand.Toggle:
                Toggle();
                return;
            case PageCommand.Close:
                Close();
                return;
            case PageCommand.Start start:
                _run.Start(start.Id);
                break;
            case PageCommand.Stop stop:
                _run.Stop(stop.Id);
                break;
            case PageCommand.Auto auto:
                _run.AutoStart(auto.Id, auto.IsOn);
                break;
            case PageCommand.AutoAt autoAt:
                PressOnScene(scene => AutoAt(scene, autoAt));
                break;
            case PageCommand.Refuel refuel:
                _run.Order = _config.Order;
                _run.Refuel(refuel.Id);
                break;
            case PageCommand.Device device:
                _run.Switch(device.Id, device.IsOn);
                break;
            case PageCommand.StartAll:
                PressOnScene(scene => _run.StartAll(scene, isChinese));
                break;
            case PageCommand.StopAll:
                PressOnScene(scene => _run.StopAll(scene, isChinese));
                break;
            case PageCommand.AutoAll autoAll:
                PressOnScene(scene => _run.AutoStartAll(scene, autoAll.IsOn, isChinese));
                break;
            case PageCommand.RefuelAll:
                _run.Order = _config.Order;
                PressOnScene(scene => _run.RefuelAll(scene, isChinese));
                break;
            case PageCommand.Low low:
                PressOnScene(scene => _store.Refusal() is { } refusal ? new Outcome(refusal.Pick(isChinese)) : _run.MarkLow(scene, low.Id, low.IsLow, isChinese));
                SaveMarks();
                break;
            case PageCommand.FuelUp up:
                ChangeOrder(_config.Order.MoveUp(up.ConfigId));
                break;
            case PageCommand.FuelDown down:
                ChangeOrder(_config.Order.MoveDown(down.ConfigId));
                break;
            case PageCommand.FuelOff off:
                ChangeOrder(_config.Order.Remove(off.ConfigId));
                break;
            case PageCommand.FuelAdd add:
                ChangeOrder(_config.Order.Add(add.ConfigId));
                break;
            case PageCommand.Setting setting:
                var message = _config.Apply(setting.Change, isChinese);
                _log($"setting {setting.Change}: {message}");
                Status(new Outcome(message));
                break;
            case PageCommand.Ready:
                _log("page script ready");
                _page.ForgetWords();
                _sentLive = null;
                _nextLiveAt = 0f;
                ReopenAfterReload();
                return;
            case PageCommand.NoScript:
                _page.Install();
                return;
            case PageCommand.Resend:
                _shownView = "";
                break;
            case PageCommand.Unknown unknown:
                _log("page said: " + unknown.Text);
                return;
            default:
                throw new InvalidOperationException("unhandled page command " + command);
        }

        // Redrawn at the next frame's refresh, so the button pressed shows its new state at once.
        _nextRefreshAt = 0f;
    }

    /// <summary>A generator's auto-start level changes; whether auto-start is on stays as it is.</summary>
    private Outcome? AutoAt(PowerScene scene, PageCommand.AutoAt autoAt)
    {
        foreach (var generator in scene.Generators)
        {
            if (generator.Id != autoAt.Id) continue;

            _run.AutoStart(generator.Id, generator.AutoStart, autoAt.At);

            return null;
        }

        return new Outcome(Texts.GeneratorGone.Pick(GameLanguage.IsChinese()));
    }

    private void PressOnScene(Func<PowerScene, Outcome?> press)
    {
        var scene = Scene();

        Report(scene == null ? new Outcome(Texts.NoGame.Pick(GameLanguage.IsChinese())) : press(scene));
    }

    private void ChangeOrder(FuelOrder order)
    {
        _config.Order = order;
        _run.Order = order;
        _log("fuel order " + order);
        Status(new Outcome(Texts.FuelListSaved.Pick(GameLanguage.IsChinese())));
        _nextFuelAt = 0f;
    }

    /// <summary>
    /// Tells the page whether a game is loaded, so the HUD button shows only then, and which key opens the panel. Sent
    /// when that changes; a reloaded page starts blank, so its ready message has it sent again.
    /// </summary>
    private void SendLive(float now)
    {
        // On its own beat, off the frames of the automatic pass and the page check (see WebPage), so their work never
        // shares a frame; a long stall starts the beat again.
        _nextLiveAt += LiveSeconds;

        if (_nextLiveAt <= now) _nextLiveAt = now + LiveSeconds;

        var isLive = GamePower.IsLive();

        if (_sentLive == isLive) return;

        if (_page.Live(isLive, KeyName()))
        {
            _sentLive = isLive;
            _log($"HUD button {(isLive ? "shown" : "hidden")}");
        }

        if (!isLive) Close();
    }

    private string KeyName() => _config.PanelKey == KeyCode.None ? "" : _config.PanelKey.ToString();

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// <summary>With no game loaded the panel stays shut and says so.</summary>
    private void Open()
    {
        if (!GamePower.IsLive())
        {
            Status(new Outcome(Texts.NoGame.Pick(GameLanguage.IsChinese())));

            return;
        }

        _isOpen = true;
        _nextFuelAt = 0f;
        InputGate.PanelOpened();
        _shownView = ReadView(Clock.Now());
        _page.Open(_shownView);
        _log("panel opened");
    }

    private void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _stock = NoStock;
        _fuelItems = NoItems;
        InputGate.PanelClosed();
        _page.Close();
    }

    /// <summary>Sends the view only when it changed, so an idle panel costs one read a second and nothing more.</summary>
    private void PushView(float now)
    {
        var view = ReadView(now);

        if (view == _shownView) return;

        if (_page.Show(view)) _shownView = view;
    }

    /// <summary>A reloaded web view starts with the panel shut, so a panel the game side thinks is open is drawn again.</summary>
    private void ReopenAfterReload()
    {
        if (!_isOpen) return;

        _shownView = ReadView(Clock.Now());
        _page.Open(_shownView);
    }

    private string ReadView(float now)
    {
        _nextRefreshAt = now + RefreshSeconds;

        var scene = Scene();

        if (now >= _nextFuelAt)
        {
            _nextFuelAt = now + FuelSeconds;
            _stock = scene == null ? NoStock : _game.Fuel();
            _fuelItems = scene == null ? NoItems : _game.FuelItems();
        }

        return PowerView.Json(scene, _run.Book.Low, _run.Book.Shed, _isRefuelling, FuelJson(), _config.Read(KeyName()));
    }

    private string FuelJson()
    {
        var order = _config.Order;
        var isChinese = GameLanguage.IsChinese();

        if (ReferenceEquals(order, _fuelJsonOrder) && ReferenceEquals(_stock, _fuelJsonStock) && ReferenceEquals(_fuelItems, _fuelJsonItems) && isChinese == _fuelJsonInChinese) return _fuelJson;

        var (listed, spare) = PowerView.FuelLines(order, _stock, _fuelItems, _itemName);

        _fuelJson = PowerView.FuelJson(listed, spare);
        _fuelJsonOrder = order;
        _fuelJsonStock = _stock;
        _fuelJsonItems = _fuelItems;
        _fuelJsonInChinese = isChinese;

        return _fuelJson;
    }

    /// <summary>Kept per language, so the fuel tab's names cost a table lookup once per item.</summary>
    private string NameOfItem(int configId)
    {
        var isChinese = GameLanguage.IsChinese();

        if (_namesInChinese != isChinese)
        {
            _names.Clear();
            _namesInChinese = isChinese;
        }

        if (_names.TryGetValue(configId, out var name)) return name;

        return _names[configId] = _game.ItemName(configId);
    }

    /// <summary>A result line, in the panel while it is open and as a toast otherwise.</summary>
    private void Status(Outcome outcome) => _page.Status(outcome.Text, _isOpen, outcome.IsError ? ToastKind.Error : ToastKind.Info);
}
