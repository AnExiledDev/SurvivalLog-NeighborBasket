using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using NeighborBasket.Adapters;

namespace NeighborBasket;

[BepInPlugin(Guid, "Neighbor Basket", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(StackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(StorageHubLink.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.neighborbasket";
    private const string Version = "0.3.0";
    private const string StackingGuid = "com.dev.survivallog.stacking";

    public override void Load()
    {
        var fillButton = Config.Bind("Basket", "FillButton", true,
            "Shows a Fill button on the neighbor's basket. It puts one cheap item for each thing she wants in the basket (the thing her trouble needs first), from your home storage and your bag, then adds more of what she wants (see FillExtra), and never more than the basket's weight limit. While she is dying it fills the basket with food instead. With Stacking installed it moves one item from a stack; without it, it moves the whole stack that costs you least.\n" +
            "在邻居的篮子上显示「装篮」按钮。它会从家中储物和背包里，为她想要的每一类物品各放一件便宜的进篮子（她遇到麻烦时所需的类别优先），再多放一些她想要的物品（见 FillExtra），且不会超过篮子的承重。她濒死时改为装满食物。安装了 Stacking 时每次只从一组中移出一件；没有安装时，整组移动总价值最低的一组。");
        var fillExtra = Config.Bind("Basket", "FillExtra", 5,
            new ConfigDescription(
                "After one item for each thing she wants, Fill adds up to this many more of the kinds she wants, the most value for their weight first, while the basket's weight allows. Items she wants count for more progress than others. 0 puts in only one of each.\n" +
                "为她想要的每一类各放一件之后，「装篮」最多再多放这么多件她想要的类别的物品，按单位重量价值从高到低，直到篮子承重用完。她想要的物品比其他物品带来更多进度。设为 0 则每类只放一件。",
                new AcceptableValueRange<int>(0, 20)));
        var putAwayButton = Config.Bind("Basket", "PutAwayButton", true,
            "Shows a Put away button on the basket while it is home, which puts what the basket holds into your home storage the way Storage Hub puts the bag away. Needs Storage Hub; without it the button does not show.\n" +
            "篮子在家时显示「收纳」按钮，把篮子里的东西收进家中储物，方式和储物中心收纳背包相同。需要储物中心模组；没有安装时不显示此按钮。");
        var callBackButton = Config.Bind("Basket", "CallBackButton", false,
            "Shows a Call back button on the basket while it is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket.\n" +
            "篮子不在家时显示「召回」按钮，按下后篮子立刻带着她的回礼回来。默认关闭，因为这会跳过游戏给篮子设定的等待时间。");
        var warningsOn = Config.Bind("Warnings", "Enabled", true,
            "Shows a warning before her condition drops a step for want of a basket, before her trouble runs out of time, and while she is struggling or dying. Each warning shows once.\n" +
            "在她因为没收到篮子而状态下降一档之前、她遇到的麻烦快要超时之前，以及她挣扎或濒死时显示提醒。每条提醒只显示一次。");
        var warnStreak = Config.Bind("Warnings", "Streak", true,
            "Warns late in the day when sending the basket today would keep the send streak going, and it has not gone out yet.\n" +
            "当天快结束而篮子还没送出、今天送出就能保住连续赠送时提醒。");
        var phoneApp = Config.Bind("Phone", "App", true,
            "Adds a Neighbor app to the phone: her standing (affinity and the good ending's mark, progress, send streak, struggles left) and a log by day of what you sent, what came back, what each send gained, and her thoughts. The log is kept per save in BepInEx/config/com.dev.survivallog.neighborbasket.gifts.\n" +
            "在手机上添加「邻居」应用：显示她的状态（好感和好结局所需、进度、连续赠送、剩余挣扎次数），以及按天记录的送出物品、送回物品、每次送出获得的进度和她的心声。记录按存档保存在 BepInEx/config/com.dev.survivallog.neighborbasket.gifts。");
        var warnHours = Config.Bind("Warnings", "WarnHours", 12,
            new ConfigDescription(
                "How many game hours ahead the warnings come.\n" +
                "提前多少游戏小时显示提醒。",
                new AcceptableValueRange<int>(1, 48)));

        var log = new BasketLog(Log, Path.Combine(Paths.BepInExRootPath, "neighbor-basket.log"));
        var page = new WebPage(log.Write);
        var journal = new GiftJournal(Path.Combine(Paths.ConfigPath, Guid + ".gifts"), log.Write);
        var session = new BasketSession(new BasketGame(), journal, page, new Toasts(page, log.Write), log.Write);

        log.Write($"===== Neighbor Basket {Version} started");

        if (!OptionalLive.IsToolkitInstalled) log.Write("Exiled's Toolkit is not installed, so toasts show on the game's page and settings are read once at start");

        var harmony = new Harmony(Guid);

        PageMessages.Apply(harmony, log.Write);
        MonologueHook.Apply(harmony, log.Write);
        OptionalLive.Watch(() =>
        {
            session.ShowFill = fillButton.Value;
            session.ShowCallBack = callBackButton.Value;
            session.ShowPutAway = putAwayButton.Value;
            session.FillExtra = fillExtra.Value;
            session.ShowApp = phoneApp.Value;
            session.AreWarningsOn = warningsOn.Value;
            session.WarnStreak = warnStreak.Value;
            session.WarnHours = warnHours.Value;
            session.Resetup();
            log.Write($"settings: fill {fillButton.Value} with {fillExtra.Value} extra, put away {putAwayButton.Value} (Storage Hub {(StorageHubLink.IsAvailable ? "found" : "not found")}), call back {callBackButton.Value}, " +
                      $"warnings {warningsOn.Value} at {warnHours.Value} h, streak {warnStreak.Value}, phone app {phoneApp.Value}");
        }, fillButton, fillExtra, putAwayButton, callBackButton, warningsOn, warnStreak, warnHours, phoneApp);
        BasketTicker.Session = session;
        BasketTicker.Log = log.Write;
        AddComponent<BasketTicker>();
    }
}
