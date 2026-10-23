# Neighbor Basket settings

Settings live in `BepInEx\config\com.dev.survivallog.neighborbasket.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## Basket

| Setting | Default | What it does |
| --- | --- | --- |
| `FillButton` | `true` | Shows the Fill button on the basket screen. It puts one item for each kind she wants that is not in the basket yet, the one that costs you least, from the storages at home and your bag, her trouble's kind first. While she is struggling or dying it puts in food instead. `false` hides it. |
| `CallBackButton` | `false` | Shows a Call back button while the basket is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket. |

## Warnings

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Warns before her condition drops a step, before her trouble runs out of time, and when she starts to struggle or is dying. Each warning shows once. |
| `WarnHours` | `12` | How many game hours ahead the warnings come, from 1 to 48. |
