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
    private const string Version = "0.4.0";
    private const string StackingGuid = "com.dev.survivallog.stacking";

    public override void Load()
    {
        var fillButton = Config.Bind("Basket", "FillButton", true,
            "Shows a Fill button in the basket station. It packs the basket from your home storage as tightly as it goes for the most progress she gives: whole stacks, placed by their shapes on the basket's grid, within its weight limit. Every kind she wants that goes in earns a bonus, so those come first, and the space left takes whatever adds the most. Your bag is never touched. While she is dying it packs food only.\n" +
            "在篮子工作台显示「装篮」按钮。它从家中储物里把篮子尽量装满，换取她给出的最多进度：按物品形状把整组摆进篮子的格子，且不超过篮子的承重。她想要的每一类放进去都有额外奖励，所以优先放这些，剩下的空间放进度最多的物品。不会动你背包里的东西。她濒死时只装食物。");
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
            session.ShowApp = phoneApp.Value;
            session.AreWarningsOn = warningsOn.Value;
            session.WarnStreak = warnStreak.Value;
            session.WarnHours = warnHours.Value;
            session.Resetup();
            log.Write($"settings: fill {fillButton.Value}, put away {putAwayButton.Value} (Storage Hub {(StorageHubLink.IsAvailable ? "found" : "not found")}), call back {callBackButton.Value}, " +
                      $"warnings {warningsOn.Value} at {warnHours.Value} h, streak {warnStreak.Value}, phone app {phoneApp.Value}");
        }, fillButton, putAwayButton, callBackButton, warningsOn, warnStreak, warnHours, phoneApp);
        BasketTicker.Session = session;
        BasketTicker.Log = log.Write;
        AddComponent<BasketTicker>();
    }
}
