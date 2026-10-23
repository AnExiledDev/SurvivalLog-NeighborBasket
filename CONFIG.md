# Neighbor Basket settings

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## Basket

| Setting | Default | What it does |
| --- | --- | --- |
| `FillButton` | `true` | Shows the Fill button in the basket station. It packs the basket from your home storage as tightly as it goes for the most progress she gives: whole stacks, placed by their shapes on the basket's grid, within its weight limit. Each kind she wants earns a bonus, so those come first, and the space left takes whatever adds the most. Your bag is never touched. While she is dying it packs food only. `false` hides it. |
| `PutAwayButton` | `true` | Shows a Put away button while the basket is home, which puts what it holds into your home storage the way Storage Hub puts the bag away. Needs Storage Hub; without it the button does not show. |
| `CallBackButton` | `false` | Shows a Call back button while the basket is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket. |

## Warnings

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Warns before her condition drops a step, before her trouble runs out of time, and when she starts to struggle or is dying. Each warning shows once. |
| `Streak` | `true` | Warns late in the day when the basket has not gone out yet and sending it today would keep the send streak going. |
| `WarnHours` | `12` | How many game hours ahead the warnings come, from 1 to 48. |

## Phone

| Setting | Default | What it does |
| --- | --- | --- |
| `App` | `true` | Adds a Neighbor app to the phone: her affinity and the good ending's mark, progress, send streak and struggles left, and a log by day of what you sent, what came back, what each send gained, and her thoughts. The log is kept per save in `BepInEx\config\com.dev.survivallog.neighborbasket.gifts`. |
