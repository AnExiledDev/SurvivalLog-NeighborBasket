Name: Neighbor Basket
Summary: Adds a card to the neighbour's gift basket with her condition, deadlines and streak, and a Fill button that puts in what she wants from your storages, then more within its weight. Warnings come before she gets worse. A phone app tracks affinity, progress and a log of every send and return. With Storage Hub, Put away empties the basket. Never sends it for you.

Description (BBCode):
Keeping a neighbour alive means opening the basket, reading what she wants, then hunting through your storages for one item of each kind before time runs out. Neighbor Basket adds a [b]Fill[/b] button to the basket screen that does the hunting, warns you before she gets worse, and adds a phone app that tracks how she is doing.

[size=5]Main features[/size]
[list]
[*][b]Basket card[/b]: above the basket panel, her condition and the hours until it drops, her trouble and its deadline, the send streak, progress and affinity, the satiety she needs while she struggles, and what Fill would add right now. The buttons sit in it.[/*]
[*][b]Fill button[/b]: for each kind she wishes for, puts in the matching item that costs you least, from the storages at home or your bag. Her trouble's kind goes in first.[/*]
[*][b]More of what she wants[/b]: after one of each kind, Fill adds up to 5 more items of the kinds she wants, the most value for their weight first.[/*]
[*][b]Never what you locked[/b]: items the basket refuses are skipped, and nothing goes past the basket's cells or its weight limit.[/*]
[*][b]Food when she is dying[/b]: while she is struggling or dying, Fill puts in food only, the most valuable first.[/*]
[*][b]Warnings[/b]: a message a few hours before her condition drops a step, before her trouble's deadline, before the send streak breaks, and when she starts to struggle or is dying. Each shows once.[/*]
[*][b]Neighbor app[/b] on the phone: her status, the same rows as the basket card (condition and when it drops, her trouble's deadline, send streak, progress and affinity), plus struggles left, and a log by day of what you sent, what came back, what each send gained, and her thoughts.[/*]
[*][b]Put away button[/b]: with Storage Hub installed, empties the basket into your home storage.[/*]
[*][b]Call back button[/b]: off by default. When on, it brings the basket home at once while it is away.[/*]
[*][b]Both neighbours[/b]: works for whichever neighbour you are helping.[/*]
[/list]
Fill never sends the basket: check what went in, then send it with the game's own button.

Works in English and Chinese.

The mod adds no data of its own to your save; the phone app's log is a separate file per save in the config folder. The moves are real, the same as moving items into the basket by hand. You can remove it at any time.

[size=5]Known issues[/size]
[list]
[*]The log only sees sends and returns while the game runs, and gifts she brings on her own are not logged. Her text messages are already in the game's Messages app.[/*]
[*]Without Stacking, the game moves a stack whole, so Fill picks the stack that costs you least in total.[/*]
[*]The most valuable food is taken to fill her most; the mod does not read how filling each food is.[/*]
[/list]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19131. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 or newer. With it, messages show as toasts and settings changed on the Mod Manager's Mods screen take effect without restarting the game. With 0.8.0 or newer, the Neighbor phone app sits in the phone's dock with the game's apps.[/*]
[*]Optional: Stacking. With it, Fill moves one unit of a stack instead of the whole stack.[/*]
[*]Optional: Storage Hub 0.13.0 or newer. With it, the Put away button shows.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\NeighborBasket.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]NeighborBasket.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.

[size=5]Configuration[/size]
Edit [font=Courier New]BepInEx\config\com.dev.survivallog.neighborbasket.cfg[/font] while the game is closed, or change it in game on the Mod Manager's Mods screen (see [url=https://github.com/AnExiledDev/SurvivalLog-NeighborBasket/blob/main/CONFIG.md]CONFIG.md[/url]; the cfg file explains each setting in English and Chinese). Settings: [font=Courier New]FillButton[/font], [font=Courier New]FillExtra[/font], [font=Courier New]PutAwayButton[/font], [font=Courier New]CallBackButton[/font], [font=Courier New]Enabled[/font] (warnings), [font=Courier New]Streak[/font], [font=Courier New]WarnHours[/font] and [font=Courier New]App[/font] (phone app).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\neighbor-basket.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].
Check out my [url=https://sldb.core-directive.com/en/]Survival Log wiki[/url] for items, crafting, maps and more.

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-NeighborBasket]github.com/AnExiledDev/SurvivalLog-NeighborBasket[/url]

[line]
[size=5]简体中文[/size]

想让邻居活下去，就得打开篮子、看她想要什么，再在储物家具里翻找每一类物品各一件，还要赶在时间用完之前。Neighbor Basket 在篮子界面加了一个「装篮」按钮替你翻找，在她状况变差之前提醒你，并在手机上添加一个记录她状况的应用。

[size=5]主要功能[/size]
[list]
[*][b]篮子信息卡[/b]：在篮子面板上方显示她的状态和距下降还剩几小时、她遇到的麻烦和期限、连续赠送、进度和好感、挣扎时所需的饱腹，以及现在「装篮」会放入什么。按钮也在卡片里。[/*]
[*][b]「装篮」按钮[/b]：为她想要的每一类物品，从家中储物家具或背包里放入对你来说最便宜的一件。她遇到的麻烦所需的类别优先放入。[/*]
[*][b]多放她想要的[/b]：每类各放一件之后，「装篮」最多再多放 5 件她想要的类别的物品，按单位重量价值从高到低。[/*]
[*][b]不动你锁定的物品[/b]：篮子拒收的物品会被跳过，放入的物品不会超过篮子的格子数和承重。[/*]
[*][b]濒死时只放食物[/b]：她挣扎或濒死时，「装篮」只放食物，价值高的优先。[/*]
[*][b]提醒[/b]：在她的状态下降一档之前几小时、她遇到的麻烦到期之前、连续赠送快要中断之前，以及她开始挣扎或濒死时显示提醒。每条只显示一次。[/*]
[*][b]「邻居」手机应用[/b]：显示她的状况，和篮子卡片相同（状况及何时下降、困境期限、连续赠送、进度和好感），外加剩余挣扎次数，以及按天记录的送出物品、送回物品、每次送出获得的进度和她的心声。[/*]
[*][b]「收纳」按钮[/b]：安装储物中心后，可把篮子里的东西收进家中储物。[/*]
[*][b]「召回」按钮[/b]：默认关闭。开启后，篮子不在家时可以让它立刻回来。[/*]
[*][b]两位邻居都支持[/b]：对你正在帮助的邻居生效。[/*]
[/list]
「装篮」不会替你送出篮子：确认放入的物品后，用游戏自己的按钮送出。

支持中文和英文。

本模组不会往存档里写入自己的数据；手机应用的记录按存档另存在配置文件夹中。物品的移动是真实的，和你亲手放进篮子一样。你可以随时移除它。

[size=5]已知问题[/size]
[list]
[*]记录只在游戏运行时看到送出和送回，她主动送来的礼物不会记录。她的短信已在游戏自带的「信息」应用中。[/*]
[*]没有安装 Stacking 时，游戏会整组移动，所以「装篮」会选总价值最低的一组。[/*]
[*]选择价值最高的食物来尽量喂饱她；本模组不读取每种食物的饱腹程度。[/*]
[/list]

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19131。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 或更新版本。安装后提示以弹窗显示，在 Mod Manager 的「模组」界面修改的设置无需重启游戏即可生效。0.8.0 或更新版本会把「邻居」手机应用和游戏自带的应用一起放在手机底部的程序栏里。[/*]
[*]可选：Stacking。安装后「装篮」每次只从一组中移出一件，而不是整组。[/*]
[*]可选：储物中心（Storage Hub）0.13.0 或更新版本。安装后显示「收纳」按钮。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\NeighborBasket.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]NeighborBasket.dll[/font]。

[size=5]配置[/size]
在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.neighborbasket.cfg[/font]，或在游戏中 Mod Manager 的「模组」界面修改。设置项：[font=Courier New]FillButton[/font]、[font=Courier New]FillExtra[/font]、[font=Courier New]PutAwayButton[/font]、[font=Courier New]CallBackButton[/font]、[font=Courier New]Enabled[/font]（提醒）、[font=Courier New]Streak[/font]、[font=Courier New]WarnHours[/font] 和 [font=Courier New]App[/font]（手机应用）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在本页的「Posts」标签里发帖。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\neighbor-basket.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。
也欢迎看看我做的 [url=https://sldb.core-directive.com/zh/]Survival Log 维基[/url]，收录物品、制作、地图等资料。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-NeighborBasket]github.com/AnExiledDev/SurvivalLog-NeighborBasket[/url]
