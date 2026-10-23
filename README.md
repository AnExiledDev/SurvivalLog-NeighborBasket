# Neighbor Basket

A Fill button on the neighbour's gift basket that puts in what she asked for, and warnings before her condition drops or her trouble runs out of time.

Keeping a neighbour alive means opening the basket, reading her wishes, then hunting through your storages for one item of each kind before the clock runs out. Neighbor Basket adds a **Fill** button to the basket screen that does the hunting: for each kind she wants, it puts in the cheapest matching item from your storages at home or your bag. It also warns you a few hours before her condition drops a step, before her trouble's deadline, and while she is struggling or dying.

## Features

- **Fill button**: on the basket screen, next to the game's send button. For each kind of item she wishes for, it puts in the matching item that costs you least, from the storages at home or your bag. Her trouble's kind goes in first, so it still fits when the basket is nearly full.
- **Never what you locked**: items the basket refuses are skipped, and nothing goes past the basket's cells.
- **Food when she is dying**: while she is struggling or dying, Fill puts in food only, the most valuable first, until the cells are full.
- **Warnings**: a message a few hours before her condition drops a step (or before she starts to struggle), before her trouble's deadline, and once when she starts to struggle or is dying. Each warning shows once.
- **Call back button**: off by default. When it is on, a **Call back** button shows while the basket is away and brings it home at once.
- **Both neighbours**: works for whichever neighbour you are helping.
- **Log file**: every press and what it moved is written to `BepInEx\neighbor-basket.log`.

Neighbor Basket adds no data of its own to your save. The moves are real: items go from your storages or bag into the basket, the same as if you had moved them by hand, and you can take them back out before you send it. You can remove the mod at any time.

## Buttons

| Button | Where | What it does |
| --- | --- | --- |
| **Fill** | Basket screen, next to the send button | Puts one item for each wished kind not yet in the basket, cheapest first, her trouble's kind first. While she is struggling or dying, food only. |
| **Call back** | Basket screen while the basket is away, only with `CallBackButton` on | Brings the basket home now. |

Fill never sends the basket. Check what went in, then send it with the game's own button.

The buttons, their tooltips and the messages follow the game's language, English or Simplified Chinese.

## Known issues and limits

- Fill puts in one item per wished kind. It does not fill the leftover cells with extra items, except food while she is dying.
- With [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking) installed, one unit of a stack goes in. Without it, the game moves a stack whole, so Fill picks the stack that costs you least in total.
- The most valuable food is taken to fill her most; the mod does not read how filling each food is.
- Before she says what she wants, Fill only puts in her trouble's kind, if she has one.
- Call back skips the trip's wait. It is off by default because it makes the game easier.

## Requirements

- Survival Log 1.1.19131. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, messages show as the Toolkit's toasts and settings changed on the Mod Manager's Mods screen take effect without restarting the game.
- Optional: [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking). With it, Fill moves one unit of a stack instead of the whole stack.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `NeighborBasket-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-NeighborBasket/releases/latest).
3. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\NeighborBasket.dll`

## Uninstall

Delete `NeighborBasket.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.neighborbasket.cfg` and `BepInEx\neighbor-basket.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## Troubleshooting

Check `BepInEx\LogOutput.log` for `===== Neighbor Basket 0.1.0 started`. The same line, plus every press and what it moved or why it moved nothing, is in `BepInEx\neighbor-basket.log`, which survives restarts.

If the Fill button does not show, open the basket from the neighbour's gift basket at home and check that `FillButton` is `true`. If Fill says nothing is at hand for a kind, nothing of that kind is in your bag or the storages at home, or all of it is locked.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\neighbor-basket.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. The .NET 8 SDK is required.

```
dotnet build src/NeighborBasket -c Release -p:GameDir="C:\path\to\Survival Log"
```

The unit tests cover the pure rules (what Fill picks, when a warning shows, the messages) and need nothing from the game:

```
dotnet test tests/NeighborBasket.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
