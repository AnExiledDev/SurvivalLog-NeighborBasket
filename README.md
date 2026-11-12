# Neighbor Basket

Neighbor Basket opens a basket station on the neighbour's gift basket, with all your home storage beside it and a Fill button that packs the basket for you. It also warns you before she gets worse, and adds a phone app that tracks how she is doing.

Keeping a neighbour alive means opening the basket, reading her wishes, then digging through your storage for one item of each kind before time runs out. Fill does the digging for you. It puts in just enough from your home storage for a big jump in her condition, using what you have most of. The warnings come a set number of hours before her condition drops a step or her trouble's deadline passes (12 by default, `WarnHours`), and again while she is struggling or dying.

## Features

- **Basket station**: opens with the basket. On the left are every storage at home and your bag, with a search box and a She wants / Everything switch, and the kinds she wants are marked. In the middle are the basket's grid, her wishes (ticked once they're in), the weight left and the progress a send would give. On the right are her condition and how many hours until it drops, her trouble and its deadline, the send streak, progress and affinity, and the satiety she needs while she struggles. Close deadlines show in orange or red.
- **Click to move**: click a stack to put one in the basket, Shift-click for the whole stack. Click a stack in the basket to put it back where it came from.
- **Fill button**: puts in enough from your home storage for 80 progress by default (`FillGoal`), the amount that lifts her condition two steps. It puts in one of each kind she wants first for its bonus, then more of what you have most of, and stops once it has enough. With Stacking it can take part of a stack, so your piles aren't emptied. Shift-click packs the basket as full as it goes for the most progress. What Fill would put in shows faintly in the free cells. Your bag is never touched.
- **Progress per item**: every stack shows the progress one more of it adds, and its tooltip also shows what the whole stack adds. Every item in a stack adds the same. The list puts the stacks that add the most at the top, and holds still while you click.
- **A cap per item**: by default Fill takes at most 30% of any one item you have at home (`FillShare`), so your last few seeds of a kind stay home. While she is dying the cap doesn't apply to food.
- **Only what fits**: Fill skips items the basket won't take, and never goes past the basket's cells or its weight limit.
- **Food when she is dying**: while she is dying, Fill packs food only, the most valuable it can fit.
- **Warnings**: a message comes a set number of hours before her condition drops a step or she starts to struggle, and before her trouble's deadline (`WarnHours`). You also get one late in the day when sending today would keep the send streak going, and one when she starts to struggle or is dying, which says how much satiety the basket needs. Each warning shows once. The most urgent one also stays as a line under Relief Progress on the game's Rescue panel until it no longer applies. Hover it to see the rest.
- **What is in the basket**: while the basket is home and holds something, a line at the top left of the screen says what is in it. During an aid event it sits on the Rescue panel, and the rest of the time in a small panel under the event list. Hover it for the full list. `OnHud` turns it off.
- **Neighbor app on the phone**: shows the same rows as the basket station (her condition and when it drops, her trouble's deadline, send streak, progress and affinity), plus struggles left and what the basket holds, even when nobody needs help. It also keeps a log by day of what you sent, what came back, what each send gained, and her thoughts.
- **Break down button**: takes apart the gifts in the basket that break down by hand (wind chimes, star jars, notes) into materials, the same as using them yourself.
- **To bag and To storage buttons**: empty the basket, her gifts included, into your bag, or (with Storage Hub installed) into your home storage.
- **Call back button**: off by default. When it is on, a **Call back** button shows while the basket is away and brings it home at once.
- **Both neighbours**: works for whichever neighbour you are helping.
- **Log file**: every press and what it moved is written to `BepInEx\neighbor-basket.log`.

Neighbor Basket adds no data of its own to your save. The phone app's log is kept in a separate file per save in `BepInEx\config\com.dev.survivallog.neighborbasket.gifts`. Every move is a real one: items go from your storage or bag into the basket, the same as if you had moved them by hand, and you can take them back out before you send it. You can remove the mod at any time.

## Buttons

| Button | Where | What it does |
| --- | --- | --- |
| **Fill** | Basket station | Puts in enough from your home storage for `FillGoal` progress (80 by default). Shift-click packs the basket as full as it goes. While she is dying, it puts in food only. |
| **Send basket** | Basket station | Sends the basket, the same as the game's Send button. |
| **To bag** | Basket station while the basket is home | Puts everything in the basket into your bag. |
| **Break down (N)** | Basket station while the basket holds gifts that break down by hand | Moves those gifts to your bag, then your character uses each one to take it apart, one after another. |
| **To storage** | Basket station while the basket is home, only with Storage Hub installed | Puts what the basket holds into your home storage. |
| **Call back** | Basket station while the basket is away, only with `CallBackButton` on | Brings the basket home now. |
| **Game screen** | Basket station | Hides the station and shows the game's basket screen until the basket is closed. |
| **Basket station** | Top right of the game's basket screen, after you press Game screen | Brings the station back. |

Fill never sends the basket. Check what went in, then press Send basket.

The buttons, their tooltips and the messages follow the game's language, English or Simplified Chinese.

## Known issues and limits

- Fill adds kinds she didn't ask for only when the kinds she wants run out before it reaches the goal, and those count for less. Click any you want to keep to take it back out.
- Without Stacking, Fill moves whole stacks, so it can go past the goal. Shift-click Fill always moves whole stacks, the most valuable it can fit.
- The log starts when the mod is first installed and only sees sends and returns while the game runs. Gifts she brings on her own are not logged. Her text messages are already in the game's Messages app.
- Clicking a stack puts one in only with [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking) installed. Without it, the whole stack goes in.
- While she is dying, Fill picks food by its value, because the mod doesn't read how filling each food is.
- Before she says what she wants, Fill only puts in her trouble's kind, if she has one.
- Call back skips the trip's wait. It is off by default because it makes the game easier.

## Requirements

- Survival Log 1.1.20719. Built for this version. A game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.19.0 or newer. Neighbor Basket does not load without it. It shows Neighbor Basket's messages as pop-ups, puts the Neighbor app in the phone's dock with the game's apps, and applies settings you change on the Mod Manager's Mods screen without a restart. Its UI size setting also sizes the basket station. Neighbor Basket's own `UiScale` under Interface sets a size for this mod alone. At 0, the default, it follows the Toolkit's.
- Optional: [Stacking](https://github.com/AnExiledDev/SurvivalLog-Stacking). With it, clicking a stack in the station puts one in instead of the whole stack, and Fill takes only the part of a stack it needs.
- Optional: [Info Pack](https://github.com/AnExiledDev/SurvivalLog-InfoPack) 0.8.13 or newer. With it, the station's stacks show Info Pack's item tooltip, with the progress line under the name.
- Optional: [Storage Hub](https://github.com/AnExiledDev/SurvivalLog-StorageHub) 0.13.0 or newer. With it, the To storage button shows.
- Optional: [Mod Manager](https://www.nexusmods.com/survivallog/mods/73). With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `NeighborBasket-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-NeighborBasket/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/92).
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\NeighborBasket.dll`

## Uninstall

Delete `NeighborBasket.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, the `BepInEx\config\com.dev.survivallog.neighborbasket.gifts` folder and `BepInEx\neighbor-basket.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Check `BepInEx\LogOutput.log` for `===== Neighbor Basket 0.8.5 started`. The same line, plus every press and what it moved or why it moved nothing, is in `BepInEx\neighbor-basket.log`, which survives restarts.

If the station doesn't show, open the neighbour's gift basket at home. If you pressed Game screen, press Basket station at the top right. If the Fill button doesn't show, check that `FillButton` is `true`. If Fill says nothing is in home storage for a kind, then the storage at home has nothing of that kind that can go in the basket.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\neighbor-basket.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. The .NET 8 SDK is required.

```
dotnet build src/NeighborBasket -c Release -p:GameDir="C:\path\to\Survival Log"
```

The unit tests cover the rules (what Fill picks, when a warning shows, the messages) and need nothing from the game:

```
dotnet test tests/NeighborBasket.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
