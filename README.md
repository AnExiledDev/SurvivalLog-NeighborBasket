# Neighbor Basket

A basket station over the neighbour's gift basket that shows every storage at home next to the basket, a Fill button that puts in just enough for a big jump in her condition, warnings before her condition drops or her trouble runs out of time, and a phone app that tracks how she is doing.

Keeping a neighbour alive means opening the basket, reading her wishes, then hunting through your storages for one item of each kind before the clock runs out. Neighbor Basket opens a **basket station** with the basket that puts all your storages beside it, and a **Fill** button that does the hunting: it puts in just enough from your home storage for a big jump in her condition, using what you have most of. It also warns you a few hours before her condition drops a step, before her trouble's deadline, and while she is struggling or dying.

## Features

- **Basket station**: opens with the basket. On the left, every storage at home and your bag, with a search box and a She wants / Everything switch; the kinds she wants are marked. In the middle, the basket's grid with what is in it, her wishes (ticked once in), the weight left and the progress a send would give. On the right, her condition and how many hours until it drops, her trouble and its deadline, the send streak, progress and affinity, and the satiety she needs while she struggles. Close deadlines show in orange or red.
- **Click to move**: click a stack to put one in the basket, Shift-click for the whole stack. Click a stack in the basket to put it back where it came from.
- **Fill button**: puts in enough from your home storage for 80 progress (`FillGoal`), the amount that lifts her condition two steps: one of each kind she wants first for its bonus, then more of what you have most of, only as many as it takes. With Stacking it takes part of a stack, so your piles are not emptied. Shift-click packs the basket as full as it goes for the most progress. What Fill would do shows faintly in the free cells. Your bag is never touched.
- **Progress per item**: every stack shows the progress one more of it adds; its tooltip shows that and the whole stack's. Each item of a stack adds the same. The list puts the stacks that add the most at the top, and holds still while you click.
- **A cap per item**: Fill takes at most 30% of any one item you have at home (`FillShare`), so your last seeds of a kind stay home. While she is dying the cap does not apply to food.
- **Never what you locked**: items the basket refuses are skipped, and nothing goes past the basket's cells or its weight limit.
- **Food when she is dying**: while she is dying, Fill packs food only, the most valuable it can fit.
- **Warnings**: a message a few hours before her condition drops a step (or before she starts to struggle), before her trouble's deadline, late in the day when today's send would keep the send streak going, and once when she starts to struggle or is dying (with how filling the basket must be). Each warning shows once.
- **Neighbor app on the phone**: her status, the same rows as the basket station (condition and when it drops, her trouble's deadline, send streak, progress and affinity), plus struggles left, and a log by day of what you sent, what came back, what each send gained, and her thoughts.
- **Break down button**: takes apart the gifts in the basket that break down by hand (wind chimes, star jars, notes), the game's own way, into materials.
- **To bag and To storage buttons**: empty the basket, her gifts included, into your bag, or (with Storage Hub installed) into your home storage.
- **Call back button**: off by default. When it is on, a **Call back** button shows while the basket is away and brings it home at once.
- **Both neighbours**: works for whichever neighbour you are helping.
- **Log file**: every press and what it moved is written to `BepInEx\neighbor-basket.log`.

Neighbor Basket adds no data of its own to your save; the phone app's log is a separate file per save in `BepInEx\config\com.dev.survivallog.neighborbasket.gifts`. The moves are real: items go from your storages or bag into the basket, the same as if you had moved them by hand, and you can take them back out before you send it. You can remove the mod at any time.

## Buttons

| Button | Where | What it does |
| --- | --- | --- |
| **Fill** | Basket station | Puts in enough from your home storage for `FillGoal` progress (80). Shift-click packs the basket as full as it goes. While she is dying, food only. |
| **Send basket** | Basket station | Sends the basket, the same as the game's own Send button. |
| **To bag** | Basket station while the basket is home | Puts everything in the basket into your bag. |
| **Break down (N)** | Basket station while the basket holds gifts that break down by hand | Moves those gifts to your bag and has your character take each apart with the game's own Use, one after another. |
| **To storage** | Basket station while the basket is home, only with Storage Hub installed | Puts what the basket holds into your home storage. |
| **Call back** | Basket station while the basket is away, only with `CallBackButton` on | Brings the basket home now. |
| **Game screen** | Basket station | Hides the station and shows the game's own basket screen until the basket is closed. |
| **Basket station** | Top right of the game's basket screen, after Game screen | Brings the station back. |

Fill never sends the basket. Check what went in, then press Send basket.

The buttons, their tooltips and the messages follow the game's language, English or Simplified Chinese.

## Known issues and limits

- Fill adds kinds she did not ask for only when the kinds she wants run out before the goal; they count for less. Take any you want to keep back out with a click.
- Without Stacking, Fill moves whole stacks, so it can go past the goal. Shift-click Fill moves whole stacks, the most valuable it can fit.
- The log starts when the mod is first installed and only sees sends and returns while the game runs. Gifts she brings on her own are not logged. Her text messages are already in the game's Messages app.
- Clicking a stack puts one in only with [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking) installed. Without it, the whole stack goes in.
- The most valuable food is taken to fill her most; the mod does not read how filling each food is.
- Before she says what she wants, Fill only puts in her trouble's kind, if she has one.
- Call back skips the trip's wait. It is off by default because it makes the game easier.

## Requirements

- Survival Log 1.1.19611. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, messages show as the Toolkit's toasts and settings changed on the Mod Manager's Mods screen take effect without restarting the game. With 0.8.0 or newer, the Neighbor phone app sits in the phone's dock with the game's apps; without it, it shows in the middle of the phone's home screen.
- Optional: [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking). With it, clicking a stack in the station puts one in instead of the whole stack, and Fill takes only the part of a stack it needs.
- Optional: [Info Pack](https://github.com/AnExiledDev/SurvivalLog-InfoPack) 0.8.13 or newer. With it, the station's stacks show Info Pack's item tooltip, with the progress line under the name.
- Optional: [Storage Hub](https://github.com/AnExiledDev/SurvivalLog-StorageHub) 0.13.0 or newer. With it, the To storage button shows.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `NeighborBasket-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-NeighborBasket/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/92).
3. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\NeighborBasket.dll`

## Uninstall

Delete `NeighborBasket.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, the `BepInEx\config\com.dev.survivallog.neighborbasket.gifts` folder and `BepInEx\neighbor-basket.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## Troubleshooting

Check `BepInEx\LogOutput.log` for `===== Neighbor Basket 0.6.3 started`. The same line, plus every press and what it moved or why it moved nothing, is in `BepInEx\neighbor-basket.log`, which survives restarts.

If the station does not show, open the basket from the neighbour's gift basket at home; if you pressed Game screen, press Basket station at the top right. If the Fill button does not show, check that `FillButton` is `true`. If Fill says nothing is in home storage for a kind, nothing of that kind is in the storages at home, or all of it is locked.

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
