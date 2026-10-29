using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private const string Version = "0.7.0";
    private const string StackingGuid = "com.dev.survivallog.stacking";
    private const string CoreNamespace = "NeighborBasket.Core";
    private const string AdaptersNamespace = "NeighborBasket.Adapters";
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public override void Load()
    {
        OptionalUiSize.Bind(Config);

        var fillButton = Config.Bind("Basket", "FillButton", true,
            "Shows a Fill button in the basket station. It puts in enough from your home storage to reach FillGoal: one of each kind she wants first (each kind earns a bonus), then more of what you have most of, only as many as it takes. With Stacking it takes part of a stack; without it, whole stacks. Shift-click packs the basket as full as it goes for the most progress, within its grid and weight limit. Your bag is never touched. While she is dying it packs food only.\n" +
            "在篮子工作台显示「装篮」按钮。它从家中储物里放入刚好达到「FillGoal」的物品：先放她想要的每一类各一件（每一类都有额外奖励），再放你最多的东西，够了就停。装有堆叠模组时可以只拿一组中的几件，没有时按整组放。按住 Shift 单击会把篮子尽量装满，换取最多的进度，且不超过篮子的格子和承重。不会动你背包里的东西。她濒死时只装食物。");
        var fillGoal = Config.Bind("Basket", "FillGoal", 80,
            new ConfigDescription(
                "The progress Fill aims for in one basket. A basket worth 40 lifts her condition one step, 80 lifts it two, so 80 gets the most from the least. Fill never aims past what her current stage still needs. 0 makes Fill pack the basket full every time.\n" +
                "「装篮」每次争取的进度。一篮达到 40 进度能让她状态提升一档，80 提升两档，所以 80 用最少的东西换最多的提升。不会超过她当前阶段还需要的进度。设为 0 则每次都把篮子装满。",
                new AcceptableValueRange<int>(0, 500)));
        var fillShare = Config.Bind("Basket", "FillShare", 30,
            new ConfigDescription(
                "The most of any one item Fill takes, in percent of how many you have at home (what is already in the basket counts). At 30, Fill takes at most 4 of 16 tomato seeds, and none of an item you have only 1 to 3 of. Shift-click Fill keeps to it too. While she is dying it does not apply, so Fill packs the food she needs. Clicking items yourself is never limited. 100 means no limit.\n" +
                "「装篮」对同一种物品最多拿走家中数量的百分之几（篮子里已有的也算）。设为 30 时，16 个番茄种子最多拿 4 个，只有 1 到 3 个的物品一个都不拿。按住 Shift 单击「装篮」也遵守此限制。她濒死时不受此限制，「装篮」会放入她需要的食物。自己单击放入物品不受限制。设为 100 表示不限制。",
                new AcceptableValueRange<int>(1, 100)));
        var putAwayButton = Config.Bind("Basket", "PutAwayButton", true,
            "Shows a To storage button on the basket while it is home, which puts what the basket holds into your home storage the way Storage Hub puts the bag away. Needs Storage Hub; without it the button does not show.\n" +
            "篮子在家时显示「放进储物」按钮，把篮子里的东西收进家中储物，方式和储物中心收纳背包相同。需要储物中心模组；没有安装时不显示此按钮。");
        var callBackButton = Config.Bind("Basket", "CallBackButton", false,
            "Shows a Call back button on the basket while it is away, which brings it home at once with whatever she sends back. Off by default, since it skips the wait the game gives the basket.\n" +
            "篮子不在家时显示「召回」按钮，按下后篮子立刻带着她的回礼回来。默认关闭，因为这会跳过游戏给篮子设定的等待时间。");
        var warningsOn = Config.Bind("Warnings", "Enabled", true,
            "Shows a warning before her condition drops a step for want of a basket, before her trouble runs out of time, and while she is struggling or dying. Each warning shows once.\n" +
            "在她因为没收到篮子而状态下降一档之前、她遇到的麻烦快要超时之前，以及她挣扎或濒死时显示提醒。每条提醒只显示一次。");
        var warnStreak = Config.Bind("Warnings", "Streak", true,
            "Warns late in the day when sending the basket today would keep the send streak going, and it has not gone out yet.\n" +
            "当天快结束而篮子还没送出、今天送出就能保住连续赠送时提醒。");
        var hudAlert = Config.Bind("Warnings", "HudAlert", true,
            "Shows the most urgent warning as a line under Relief Progress on the game's Rescue panel, with the hours left, red when her life is at stake. Hover it to see every warning. Unlike the toasts, it stays up for as long as the warning is due.\n" +
            "在游戏救助面板的救助进度下方显示一行最紧急的提醒和剩余小时数，危及她性命时为红色。鼠标悬停可查看全部提醒。与弹出提醒不同，这一行会一直显示，直到提醒不再需要。");
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
        var session = new BasketSession(new BasketGame(log.Write), journal, page, new Toasts(page, log.Write), log.Write);

        log.Write($"===== Neighbor Basket {Version} started");

        // The station, Fill and phone builds compile on first use; doing that here keeps it off the first basket opened.
        // The plain part runs once on made-up data, and the rest of the mod's code is compiled without being run, so
        // nothing on this thread touches the game.
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Core.WarmUp.Run();

                foreach (var type in typeof(Plugin).Assembly.GetTypes())
                {
                    if (type.Namespace is CoreNamespace or AdaptersNamespace && !typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(type)) Prepare(type);
                }
            }
            catch (Exception)
            {
                // Only a head start; the first real build compiles them anyway.
            }
        });

        if (!OptionalLive.IsToolkitInstalled) log.Write("Exiled's Toolkit is not installed, so toasts show on the game's page and settings are read once at start");

        var harmony = new Harmony(Guid);

        PageMessages.Apply(harmony, log.Write);
        MonologueHook.Apply(harmony, log.Write);
        OptionalLive.Watch(() =>
        {
            session.ShowFill = fillButton.Value;
            session.FillGoal = fillGoal.Value;
            session.FillShare = fillShare.Value;
            session.ShowCallBack = callBackButton.Value;
            session.ShowPutAway = putAwayButton.Value;
            session.ShowApp = phoneApp.Value;
            session.AreWarningsOn = warningsOn.Value;
            session.WarnStreak = warnStreak.Value;
            session.WarnHours = warnHours.Value;
            session.ShowHudAlert = hudAlert.Value;
            session.Resetup();
            log.Write($"settings: fill {fillButton.Value} to {fillGoal.Value} share {fillShare.Value}%, put away {putAwayButton.Value} (Storage Hub {(StorageHubLink.IsAvailable ? "found" : "not found")}), call back {callBackButton.Value}, " +
                      $"warnings {warningsOn.Value} at {warnHours.Value} h, streak {warnStreak.Value}, HUD alert {hudAlert.Value}, phone app {phoneApp.Value}");
        }, fillButton, fillGoal, fillShare, putAwayButton, callBackButton, warningsOn, warnStreak, warnHours, hudAlert, phoneApp);
        BasketTicker.Session = session;
        BasketTicker.Log = log.Write;
        AddComponent<BasketTicker>();
    }

    /// <summary>
    /// Compiles every method the type declares without running any. GetTypes lists nested types on their own, so the
    /// lambdas and records inside a type get theirs too. The game's components are left to the game.
    /// </summary>
    private static void Prepare(Type type)
    {
        if (type.ContainsGenericParameters) return;

        foreach (var method in type.GetMethods(Declared))
        {
            if (!method.IsAbstract && !method.ContainsGenericParameters) RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }
}
