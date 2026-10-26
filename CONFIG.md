# Neighbor Basket settings

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## Basket

| Setting | Default | What it does |
| --- | --- | --- |
| `FillButton` | `true` | Shows the Fill button in the basket station. It puts in enough from your home storage to reach `FillGoal`: one of each kind she wants first (each kind earns a bonus), then more of what you have most of, only as many as it takes. With Stacking it takes part of a stack; without it, whole stacks. Shift-click packs the basket as full as it goes for the most progress, within its grid and weight limit. Your bag is never touched. While she is dying it packs food only. `false` hides it. |
| `FillGoal` | `80` | The progress Fill aims for in one basket, 0 to 500. A basket worth 40 lifts her condition one step, 80 lifts it two, so 80 gets the most from the least. Fill never aims past what her current stage still needs. `0` makes Fill pack the basket full every time. |
| `FillShare` | `30` | The most of any one item Fill takes, in percent of how many you have at home (what is already in the basket counts), 1 to 100. At 30, Fill takes at most 4 of 16 tomato seeds, and none of an item you have only 1 to 3 of. Shift-click Fill keeps to it too. While she is dying it does not apply, so Fill packs the food she needs. Clicking items yourself is never limited. 100 means no limit. |
| `PutAwayButton` | `true` | Shows a To storage button while the basket is home, which puts what it holds into your home storage the way Storage Hub puts the bag away. Needs Storage Hub; without it the button does not show. |
| `CallBackButton` | `false` | Shows a Call back button while the basket is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket. |

## Warnings

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Warns before her condition drops a step, before her trouble runs out of time, and when she starts to struggle or is dying. Each warning shows once. |
| `Streak` | `true` | Warns late in the day when the basket has not gone out yet and sending it today would keep the send streak going. |
| `HudAlert` | `true` | Shows the most urgent warning as a line under Relief Progress on the game's Rescue panel, red when her life is at stake. Hover it to see every warning. |
| `WarnHours` | `12` | How many game hours ahead the warnings come, from 1 to 48. |

## Phone

| Setting | Default | What it does |
| --- | --- | --- |
| `App` | `true` | Adds a Neighbor app to the phone: her affinity and the good ending's mark, progress, send streak and struggles left, and a log by day of what you sent, what came back, what each send gained, and her thoughts. The log is kept per save in `BepInEx\config\com.dev.survivallog.neighborbasket.gifts`. |
