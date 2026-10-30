# Power Manager settings

Settings live in `BepInEx\config\com.dev.survivallog.powermanager.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Changed on the Mods screen, every setting takes effect at once except `BatteryMultiplier`, which takes effect at the next game start. `AutoRefuel`, `AutoRefuelMinutes`, `ShedBelow` and the fuel order can also be changed in the panel, which saves to this file at once. Delete the file to get the defaults back. Every default in the Power and Solar sections is the game's own rule.

The first time the game starts with Power Manager, the power settings you changed in Game Rules (`[Power]` and `[Solar]`) and Machine Speed (`GeneratorEfficiency` and `FuelBurn`) are copied into this file. Those mods no longer have these settings. Their own files are only read, never changed.

## Controls

| Setting | Default | What it does |
| --- | --- | --- |
| `PanelKey` | `F2` | Key that opens and closes the panel. `None` turns the key off; the lightning button in the bottom-left corner of the screen still opens the panel. |

The value is a Unity key name, for example `F2`, `F11`, `Insert` or `Keypad5`. `Esc` always closes the open panel and cannot be changed.

## Generators

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoRefuel` | `false` | Refuels a running generator, or one set to auto-start, from home storage when it has less than `AutoRefuelMinutes` of fuel left, using the fuel order. |
| `AutoRefuelMinutes` | `60` | With `AutoRefuel` on, a generator with less than this many in-game minutes of fuel is refuelled. 10 to 1440. |
| `GeneratorEfficiency` | `1` | How much power fuel generators make from the same fuel: 2 is twice the power from each unit of fuel. 0.1 to 100. |
| `FuelBurn` | `1` | How fast fuel generators burn fuel: 2 burns it twice as fast, 0.5 makes it last twice as long. 0.25 to 100000. The fuel time the game and the panel show changes to match. |

## Shedding

| Setting | Default | What it does |
| --- | --- | --- |
| `ShedBelow` | `20` | During a power cut, devices you mark low priority in the panel switch off when the battery falls below this percent, and back on once it is 10 points above. 0 turns this off. 0 to 90. |

No device is low priority until you mark it in the panel, so nothing is switched off until you do.

## Fuel

| Setting | Default | What it does |
| --- | --- | --- |
| `Order` | junk paper, then alcohol, then diesel | The items used to refuel generators, by item id, first used first. Items not on the list are never used. Easiest to change on the panel's Fuel tab. |

The default list starts with notes, letters, flyers and other paper with no other use, then Poor-Quality Alcohol, Common Alcohol and High-Proof Alcohol, then Diesel, so the best fuel is kept for last. Scrap Paper, Newspaper, Entertaining Magazine, Wood Chips and Cardboard are not on it; add them on the Fuel tab if you want them burned. The Fuel tab can add any item that burns, and shows each item's burn value.

## Power

| Setting | Default | What it does |
| --- | --- | --- |
| `BatteryMultiplier` | `1` | Batteries hold this many times the game's power. 0.1 to 10. Takes effect at the next game start. |
| `DevicePowerUse` | `1` | Power that switched-on devices use, times the game's. 0 to 3. Cooking and shredding jobs, the compressor, defenses firing and the signal station are unchanged. |
| `PowerCutDelay` | `0` | Days later than in the game that the city power is cut. 0 to 100. |
| `NoPowerCut` | `false` | The city power is never cut. The events that follow the cut, and the diesel generator start, never come. A cut that already happened is not undone. |
| `NoBlackoutPanic` | `false` | Power cuts at night no longer bring Blackout panic; a panic already running clears within ten in-game minutes. |

## Solar

| Setting | Default | What it does |
| --- | --- | --- |
| `SolarMultiplier` | `1` | Solar panels make this many times the game's power. 0.1 to 10. |
| `BadWeatherOutput` | `0` | In storms and heavy rain the game's panels make nothing. This gives them a share of a cloudy day's power: 0.5 is half, 1 is a full cloudy day. 0 to 1. The multiplier applies on top. |

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | Size of the panel and its messages, in percent, from 75 to 200. At 0 it follows Exiled's Toolkit's UI size. |
