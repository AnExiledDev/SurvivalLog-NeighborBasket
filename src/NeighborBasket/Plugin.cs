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
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.neighborbasket";
    private const string Version = "0.1.0";
    private const string StackingGuid = "com.dev.survivallog.stacking";

    public override void Load()
    {
        var fillButton = Config.Bind("Basket", "FillButton", true,
            "Shows a Fill button on the neighbor's basket. It puts one cheap item for each thing she wants in the basket (the thing her trouble needs first), from your home storage and your bag, and never more than the basket holds. While she is dying it fills the basket with food instead. With Stacking installed it moves one item from a stack; without it, it moves the whole stack that costs you least.\n" +
            "在邻居的篮子上显示「装篮」按钮。它会从家中储物和背包里，为她想要的每一类物品各放一件便宜的进篮子（她遇到麻烦时所需的类别优先），且不会超过篮子的容量。她濒死时改为装满食物。安装了 Stacking 时每次只从一组中移出一件；没有安装时，整组移动总价值最低的一组。");
        var callBackButton = Config.Bind("Basket", "CallBackButton", false,
            "Shows a Call back button on the basket while it is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket.\n" +
            "篮子不在家时显示「召回」按钮，按下后篮子立刻带着她的回礼回来。默认关闭，因为这会跳过游戏给篮子设定的等待时间。");
        var warningsOn = Config.Bind("Warnings", "Enabled", true,
            "Shows a warning before her condition drops a step for want of a basket, before her trouble runs out of time, and while she is struggling or dying. Each warning shows once.\n" +
            "在她因为没收到篮子而状态下降一档之前、她遇到的麻烦快要超时之前，以及她挣扎或濒死时显示提醒。每条提醒只显示一次。");
        var warnHours = Config.Bind("Warnings", "WarnHours", 12,
            new ConfigDescription(
                "How many game hours ahead the warnings come.\n" +
                "提前多少游戏小时显示提醒。",
                new AcceptableValueRange<int>(1, 48)));

        var log = new BasketLog(Log, Path.Combine(Paths.BepInExRootPath, "neighbor-basket.log"));
        var page = new WebPage(log.Write);
        var session = new BasketSession(new BasketGame(), page, new Toasts(page, log.Write), log.Write);

        log.Write($"===== Neighbor Basket {Version} started");

        if (!OptionalLive.IsToolkitInstalled) log.Write("Exiled's Toolkit is not installed, so toasts show on the game's page and settings are read once at start");

        PageMessages.Apply(new Harmony(Guid), log.Write);
        OptionalLive.Watch(() =>
        {
            session.ShowFill = fillButton.Value;
            session.ShowCallBack = callBackButton.Value;
            session.AreWarningsOn = warningsOn.Value;
            session.WarnHours = warnHours.Value;
            session.Resetup();
            log.Write($"settings: fill {fillButton.Value}, call back {callBackButton.Value}, warnings {warningsOn.Value} at {warnHours.Value} h");
        }, fillButton, callBackButton, warningsOn, warnHours);
        BasketTicker.Session = session;
        BasketTicker.Log = log.Write;
        AddComponent<BasketTicker>();
    }
}
