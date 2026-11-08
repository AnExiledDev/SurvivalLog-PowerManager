# Changelog

## 0.5.0

- With [Wind Power](https://www.nexusmods.com/survivallog/mods/136) 0.9.4 or newer installed, a new `SmogOutput` setting gives Heavy Smog some wind, so the turbines turn and make power there, the way `BadWeatherOutput` does for solar panels in storms. It is a share of a Clear day's wind, takes effect at once, and each world can keep its own. It starts at 0, so nothing changes until you set it.
- Made for Survival Log 1.1.20719.

## 0.4.0

- With [Wind Power](https://www.nexusmods.com/survivallog/mods/136) 0.9.3 or newer installed, a new `WindMultiplier` setting makes its wind turbines make more or less power, the way `SolarMultiplier` does for solar panels. It takes effect at once, and each world can keep its own. Without Wind Power the setting does not show.
- Made for Survival Log 1.1.20719.

## 0.3.1

- Now uses Exiled's Toolkit for its shared parts, so it needs Exiled's Toolkit 0.19.0 or newer.
- Made for Survival Log 1.1.20719.

## 0.3.0

- Each world can keep its own power settings: every setting in Power and Solar, plus `GeneratorEfficiency` and `FuelBurn`. With Exiled's Toolkit 0.17.0 or newer, pick them for a new world in Customizable Difficulty, or change them for the world you are playing on the Mod Manager's Mods screen. The panel, refuel, shedding and fuel order settings stay the same for every world.
- `BatteryMultiplier` now takes effect at once, without a restart. Lowering it drops any stored power above the new size.
- The Mods screen no longer says the panel key, refuel, shedding and fuel order settings need a restart. They never did.
- Made for Survival Log 1.1.20151.

## 0.2.2

- **Warning:** needs Exiled's Toolkit 0.12.0 or newer. Update the Toolkit first.
- With Game Rules or Machine Speed older than 0.7.0 installed, Power Manager now leaves their power settings to them and a message asks you to update them. Before, both mods applied the same settings, so device power, battery size and generator settings counted twice.
- A carried-over setting the mod could not read is now logged as not carried.
- Made for Survival Log 1.1.20151.

## 0.2.1

- The panel key is now unbound by default; use the HUD button, or set a key in the settings.
- The panel key no longer opens or closes the panel while you type in a text box.
- Exiled's Toolkit 0.12.0 or newer is now required.
- Made for Survival Log 1.1.20151.

## 0.2.0

- The default fuel order no longer burns Scrap Paper, Newspaper, Entertaining Magazine, Wood Chips or Cardboard. It now burns Poor-Quality Alcohol, Common Alcohol and High-Proof Alcohol, weakest first, before Diesel. If you already play with Power Manager, your saved fuel order stays as it is.
- You can now add any item that burns to the fuel order, not only items in your storage. Items already on the fuel order are not offered again.
- An item you remove from the fuel order shows up in the add list right away, so you can add it back without closing the panel.
- Every item on the Fuel tab shows its burn value, so you can see which fuel lasts longest.
- Made for Survival Log 1.1.20151.

## 0.1.0

- First release: one panel for your home's power, on `F2` or a lightning button in the bottom-left corner of the screen.
- Start and stop every fuel generator from the panel, one at a time or all at once, and set each one's auto-start.
- Refuel generators from home storage in a fuel order you set, junk paper first. Items left off the list are never burned.
- Auto-refuel, off by default, tops up a running generator, or one set to auto-start, when its fuel runs low.
- A battery forecast shows how long until the battery is empty or full at the current use.
- Load shedding: devices you mark low priority switch off when the battery runs low during a power cut, and back on once it recovers.
- The power and solar settings from Game Rules and the generator settings from Machine Speed now live here. Values you changed in those mods are carried over the first time the game starts with Power Manager.
- Made for Survival Log 1.1.19611.
