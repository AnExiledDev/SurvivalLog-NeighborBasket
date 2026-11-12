# Changelog

## 0.8.4

- Fixed: messages about a single stack or gift said "1 stacks", "1 of 1 stacks" or "1 of 1 gifts". They now say "1 stack" or "1 gift".
- Made for Survival Log 1.1.20719.

## 0.8.3

- Clearer wording in the settings, the warnings and the basket station.
- Made for Survival Log 1.1.20719.

## 0.8.2

- Now needs Exiled's Toolkit 0.19.0 or newer, because some of the mod's code moved into the Toolkit.
- Made for Survival Log 1.1.20719.

## 0.8.1

- **Warning:** needs Exiled's Toolkit 0.11.0 or newer. Update the Toolkit first.
- Fixed: a basket made bigger by another mod (such as Pack Rat) shrank the whole basket station to fit the screen. The basket now scrolls in its own space, and the rest of the station keeps its size.
- Made for Survival Log 1.1.20151.

## 0.8.0

- The Neighbor phone app now always has a Basket row: what the basket holds, Empty, or that it is still with her. It shows even when nobody needs help.
- New: while the basket is home and holds something, a line at the top left of the screen says what is in it. During an aid event it sits on the Rescue panel, and the rest of the time in a small panel under the event list. Hover it for the full list. `OnHud` under Basket turns it off.
- Made for Survival Log 1.1.20151.

## 0.7.2

- Fixed: typing into the basket station's search box did nothing.
- Made for Survival Log 1.1.20151.

## 0.7.1

- Neighbor Basket's messages now always show as Exiled's Toolkit pop-ups. They also go to the Notifications history.
- Exiled's Toolkit 0.11.0 or newer is now required.
- Made for Survival Log 1.1.19611.

## 0.7.0

- New: `UiScale` under Interface lets you size Neighbor Basket's windows on their own, from 75% to 200%, separate from Exiled's Toolkit's UI size. At 0, the default, they follow the Toolkit's. Needs Exiled's Toolkit 0.11.0 or newer.
- Made for Survival Log 1.1.19611.

## 0.6.10

- Fixed: the game could stutter for a moment the first time you opened the basket in a game.
- Made for Survival Log 1.1.19611.

## 0.6.9

- The basket station can be drawn bigger or smaller with the UI size setting of Exiled's Toolkit 0.10.0 or newer. A change shows at once. If the size you pick is too big for your screen, the station shows as big as it can.
- The Neighbor phone app, the line on the Rescue panel and the Basket station button keep the game's size.
- Made for Survival Log 1.1.19611.

## 0.6.8

- Less stutter with the inventory open, and the inventory no longer pauses when it opens.
- The Neighbor app opens faster on a long log: her status and the newest entries show at once, and the rest follows a moment later.
- Made for Survival Log 1.1.19611.

## 0.6.7

- The phone runs smoother once the Neighbor app is on it.
- Made for Survival Log 1.1.19611.

## 0.6.6

- The Neighbor app is on the phone the moment the phone opens, instead of showing up a moment later.
- Made for Survival Log 1.1.19611.

## 0.6.5

- The basket station opens every time now. Before, closing and opening the basket quickly could leave the game's screen up instead.
- The station opens at once and only redraws what changed, so clicking items, searching and scrolling no longer stutter with a lot of storage.
- Fill shows its stacks landing in the basket as soon as you click, and finishes the moment they are in instead of after a fixed wait.
- Made for Survival Log 1.1.19611.

## 0.6.4

- The game's Rescue panel now shows the most urgent warning under Relief Progress, with the hours left. It is red when her life is at stake (struggling, dying, or about to struggle) and amber otherwise, and it stays up until the warning no longer applies. When more than one is due it says how many more. Hover the line to see them all.
- New setting `HudAlert` (on by default) turns the line on or off.
- Made for Survival Log 1.1.19611.

## 0.6.3

- With Stacking, Fill now puts in everything it planned from a stack in one press. Before, it moved only one item from each stack it split, so you had to press Fill over and over.
- Adding one item from a stack at the station no longer greys out its row. The rest of the stack stays on the same row, so you can keep clicking it.
- Made for Survival Log 1.1.19611.

## 0.6.2

- While she is dying, the FillShare cap no longer holds back her food. Before, a kind of food kept in one big stack could be left out completely, and Fill said there was none at home.
- When the cap holds back everything Fill could use, it says so instead of saying nothing of that kind is at home.
- Break down no longer cancels what your character is doing when the game wouldn't normally interrupt it.
- Only items that don't stack count as gifts you can break down.
- If a gift could not be broken down after it went to your bag, the message now says it is in your bag.
- Made for Survival Log 1.1.19611.

## 0.6.1

- New Break down button at the basket station. It shows when the basket holds gifts you can take apart by hand (wind chimes, star jars, notes and the like), and takes them apart the same way you would: each goes to your bag, then your character uses it, one after another, taking the usual time. Only items whose Use gives back nothing but materials count, so food, flowers and packages are never touched.
- Made for Survival Log 1.1.19611.

## 0.6.0

- Fill takes at most 30% of any one item you have at home (`FillShare`), so it no longer gives away all your tomato seeds. Items you have only 1 to 3 of stay home. Shift-click Fill keeps to the limit too, and clicking items in yourself is never limited.
- New To bag button: puts everything in the basket into your bag, her gifts included. Put away is now called To storage.
- The station's list puts the stacks that add the most progress at the top.
- The list holds still while you click: a stack you put in whole keeps its row, greyed out, so the rows below do not move under the mouse. It sorts again when you change the place, tab or search, or send the basket.
- Made for Survival Log 1.1.19611.

## 0.5.0

- Fill now stops at just enough for a big jump in her condition (80 progress, `FillGoal`). Before, it filled the basket. It puts in one of each kind she wants first for its bonus, then more of what you have most of. With Stacking it takes part of a stack, so it no longer empties your piles. It never aims past what her current stage still needs. Shift-click Fill still packs the basket full.
- Every stack in the station shows the progress one more of it adds, and its tooltip shows that and the whole stack's.
- With Info Pack 0.8.13 or newer, the station's stacks show Info Pack's item tooltip, with the progress line under the name.
- Clicking a stack to put it in or take it out shows at once, so you can click quickly.
- Made for Survival Log 1.1.19611.

## 0.4.0

- New basket station: opening the neighbour's basket now shows every storage at home and your bag on the left, the basket's 5 x 3 grid with her wishes, its weight and the progress a send would give in the middle, and her status on the right. Click a stack to put one in (Shift-click for the whole stack), and click a stack in the basket to put it back where it came from. Send basket sends it the same as the game's Send button. Game screen goes back to the game's basket screen, and Basket station brings the station back.
- Fill now packs the basket from your home storage as tightly as it goes for the most progress: whole stacks, placed by their shapes on the grid, within the weight limit. Each kind she wants earns its bonus first, then the space left takes whatever adds the most. The plan shows faintly in the free cells before you press it. Your bag is never touched.
- The `FillExtra` setting is gone, since Fill now fills the basket.
- The basket card is replaced by the station.

## 0.3.1

- The Neighbor phone app shows her status at the top, the same rows as the basket card, with close deadlines in orange or red, so you can check on her without opening the basket.

## 0.3.0

- New card above the basket panel: her condition and how many hours until it drops, her trouble and its deadline, the send streak and whether today is sent, progress and affinity, the satiety she needs while she struggles, and what Fill would add right now. Close deadlines show in orange or red.
- Fill, Put away and Call back moved from the game's footer into the card, so the game's hint and Send button have their room back.
- With Exiled's Toolkit 0.8.0 or newer, the Neighbor phone app sits in the phone's dock with the game's apps instead of in the middle of the home screen.

## 0.2.0

- Fill now adds more of what she wants after one of each kind (5 by default, `FillExtra`), the most value for their weight first, and stays within the basket's weight limit.
- New warning late in the day when today's send would keep the send streak going (`Streak`).
- The struggle warning now says how much satiety the basket needs, and Fill tells you how much the basket holds while she struggles.
- New Neighbor app on the phone: her affinity and how much the good ending needs, progress, send streak and struggles left, and a log by day of what you sent, what came back, what each send gained, and her thoughts.
- New Put away button while the basket is home, which empties it into your home storage. Needs Storage Hub 0.13.0 or newer. Without it the button does not show.

## 0.1.0

First release. A Fill button on the neighbour's gift basket puts in one item for each kind she wants, the cheapest at hand, her trouble's kind first, and food while she is dying. Warnings come before her condition drops, before her trouble's deadline, and while she is struggling or dying. An optional Call back button brings the basket home early. Made for Survival Log 1.1.19131.
