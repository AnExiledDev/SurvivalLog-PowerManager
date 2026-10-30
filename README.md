# Power Manager

One panel for your home's power: generators, fuel, battery and devices.

Running your home's power means walking to each generator to start it, stop it and feed it fuel, and guessing how long the battery will last. Power Manager puts it all in one panel you can open from anywhere: start and stop your generators, set their auto-start, refuel them from home storage in an order you choose (junk paper first), see how long the battery lasts, and have low-priority devices switch off by themselves when the battery runs low during a power cut.

## Features

- **Panel key**: unbound by default. Set `PanelKey` in the config to open and close the panel from the keyboard in a loaded game, past the prologue. `Esc` or the close button closes it. The key does nothing while you type in a text box.
- **HUD button**: a lightning bolt at the top of the tool column in the bottom-left corner of the screen. It opens and closes the panel. It only shows in a loaded game past the prologue.
- **Battery and forecast**: the Power tab shows how much the battery holds, what your home makes and uses every 10 minutes, and how long until the battery is empty or full at that rate. While the city power is on it says the battery is not in use.
- **Generators**: every fuel generator at home, whether it is running, how much fuel time it has left and how many fuel slots are filled. **Start**, **Stop** and **Refuel** for each one, and **Start all**, **Stop all** and **Refuel all** for every one.
- **Auto-start**: turn on the game's own auto-start for a generator and pick the battery level it starts below, or set it for every generator at once. It is the same setting as the generator's own menu.
- **Refuel from home storage**: Refuel takes fuel from your home's storage and fills the generator, in your fuel order. The neighbor's basket and stoves are left alone.
- **Fuel order**: the Fuel tab lists the items used as fuel, top first, with how many of each are in storage. Each item shows its burn value. Move them up and down, remove them, or add any other item that burns, whether you have it or not. The add list leaves out what is already on the fuel list, and an item you remove shows up there right away. Items left off the list are never burned, so food and tools stay where they are. The default list burns notes, letters, flyers and other junk paper first, then Poor-Quality, Common and High-Proof Alcohol, then Diesel last.
- **Auto-refuel**: turn it on in the Settings tab and a running generator, or one set to auto-start, is topped up from storage when it has less than 60 in-game minutes of fuel left (you can change the minutes). When your storage has nothing from the fuel list, a message says so once.
- **Devices**: every powered device at home, with how much power it uses and an **On**/**Off** switch.
- **Load shedding**: mark devices as **Low priority**. During a power cut, when the battery falls below 20% (you can change it), those devices switch off, and they switch back on once the battery is 10 points above that. Only devices the mod switched off are switched back on.
- **Settings tab**: auto-refuel, its minutes and the load shedding level can be changed in the panel and are saved to the config at once. The panel key shows there but is changed in the config.
- **Power and solar settings**: battery size, how much power devices use, when the city power is cut or that it never is, Blackout panic, solar panel output and output in bad weather, generator power and fuel burn are all settings of this mod (see [CONFIG.md](CONFIG.md)). These used to be in Game Rules and Machine Speed; values you set there are carried over the first time the game starts with Power Manager.
- **Clear messages**: after each press a line says what happened. When the panel is closed, messages such as auto-refuel and load shedding show as pop-ups.
- **Game hotkeys paused**: the game's own hotkeys are off while the panel is open and come back when it closes.
- **UI size**: Power Manager's own `UiScale` under Interface sets the size of the panel, from 75% to 200%. At 0, the default, it follows Exiled's Toolkit's UI size.
- **Log file**: every press and what came of it is written to `BepInEx\power-manager.log`.

Every action is the game's own: the same start, stop, auto-start, refuel and switch the game's menus use. Refuelling uses the items up, as if you had put them in by hand.

Power Manager writes nothing into your save file. Which devices are low priority is kept for each save in `BepInEx\config\com.dev.survivallog.powermanager.devices.txt`. You can remove the mod at any time; generators and devices stay as they are.

## Hotkeys

| Key | What it does |
| --- | --- |
| `Esc` | Closes the panel. |

The panel key (`PanelKey`) is unbound by default: the HUD button opens the panel. Set a key in the config to open it from the keyboard too. It does nothing while you type in a text box.

The mod's panel and messages follow the game's language: English or Simplified Chinese. Generator, device and item names come from the game.

## Known issues and limits

- Stopping a generator also turns its auto-start off. This is the game's own rule; turn auto-start back on in the panel.
- While the game holds its power switches (during some story events), nothing can be switched and the panel says so.
- Devices that Defense Manager powers are shown but cannot be switched or marked low priority here.
- A low-priority device you switch back on while the battery is still low is switched off again. Mark it normal priority to keep it on.
- Device priorities belong to a save file, so a new game needs to be saved once before devices can be marked low priority.
- `BatteryMultiplier` takes effect at the next game start.
- Fuel heaters are not generators and are not listed.

## Requirements

- Survival Log 1.1.20151. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.12.0 or newer. Power Manager does not load without it. It keeps the panel key quiet while you type, shows messages as pop-ups while the panel is closed, applies settings changed on the Mod Manager's Mods screen without a restart, and lays out the HUD's tool buttons.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `PowerManager-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-PowerManager/releases/latest).
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\PowerManager.dll`

If you use Game Rules or Machine Speed, update them to 0.7.0 or newer too: their power settings have moved here. Until you do, Power Manager leaves those settings to the old version and a message asks you to update it.

## Uninstall

Delete `PowerManager.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.powermanager.cfg`, `BepInEx\config\com.dev.survivallog.powermanager.devices.txt` and `BepInEx\power-manager.log`.

Devices the mod switched off to save the battery stay off; switch them on from the game's own power panel.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.powermanager.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Changed on the Mods screen, every setting takes effect at once except `BatteryMultiplier`, which takes effect at the next game start. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Check `BepInEx\LogOutput.log` for `===== Power Manager 0.2.2 started, panel key None, battery 1x the game's`. The same line, plus one for every press and what came of it, is in `BepInEx\power-manager.log`, which survives restarts.

If the panel key does nothing, check that you set one in the config (it is unbound by default), that you are in a loaded game past the prologue, that you are not typing in a text box, and that no other mod uses the same key. The HUD button works either way.

If Refuel adds nothing, open the Fuel tab: only items on the list are used, and the count beside each shows how many are in home storage.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\power-manager.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/PowerManager -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The output DLL is at `src\PowerManager\bin\Release\PowerManager.dll`.

The unit tests cover the pure rules (the forecast, load shedding, auto-refuel, the fuel order, the panel's commands and page, the messages and the carried-over settings) and need nothing from the game:

```
dotnet test tests/PowerManager.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
