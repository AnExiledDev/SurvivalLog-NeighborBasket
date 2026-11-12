using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class TextsTests
{
    private static readonly Dictionary<int, string> Names = new() { [4] = "Tools", [6] = "Cloth" };

    private static Candidate Item(long id, int category) => new(id, 1, 1, category, 5, 1, false, 0, 1, 1);

    [Fact]
    public void English_text_is_plain_ascii_and_every_phrase_has_a_chinese_copy()
    {
        foreach (var phrase in Texts.All)
        {
            Assert.All(phrase.En, character => Assert.InRange(character, ' ', '~'));
            Assert.False(string.IsNullOrWhiteSpace(phrase.Zh));
            Assert.Equal(phrase.En.Count(character => character == '{'), phrase.Zh.Count(character => character == '{'));
        }
    }

    [Fact]
    public void The_fill_toast_says_how_many_went_in_the_gain_and_what_storage_lacks()
    {
        var result = new FillResult(new[] { new Pick(Item(1, 4), 0, 0) }, new[] { 6 }, FillStop.None, false, 17);

        Assert.Equal("Put 1 in the basket, about +17 progress. Nothing in home storage for: Cloth.", Texts.Outcome(result, 0, Names, false));
    }

    [Fact]
    public void Refused_moves_are_counted_apart_from_the_ones_that_went_in()
    {
        var result = new FillResult(new[] { new Pick(Item(1, 4), 0, 0), new Pick(Item(2, 6), 1, 0) }, new int[0], FillStop.None, false, 34);

        Assert.Equal("Put 1 in the basket, about +34 progress. 1 could not be moved.", Texts.Outcome(result, 1, Names, false));
    }

    [Fact]
    public void An_unnamed_category_shows_its_number()
    {
        var result = new FillResult(new Pick[0], new[] { 12 }, FillStop.None, false);

        Assert.Equal("Nothing in home storage for: #12.", Texts.Outcome(result, 0, Names, false));
    }

    [Fact]
    public void Warnings_carry_the_hours_left()
    {
        Assert.Equal("About 7 h until her condition drops a step. Send the basket before then.", Texts.Warn(new Warning(WarningKind.TierDrop, 7, "k"), false));
        Assert.Contains("7", Texts.Warn(new Warning(WarningKind.Struggle, 7, "k"), true));
    }

    [Fact]
    public void A_struggle_warning_names_the_satiety_she_needs()
    {
        Assert.Equal(
            "She is struggling. About 5 h left to send her food: at least 10 satiety pulls her through, 100 or more for a better recovery.",
            Texts.Warn(new Warning(WarningKind.Struggle, 5, "k", 10, 100), false));
    }

    [Fact]
    public void The_streak_warning_says_how_far_it_has_come_and_the_hours_left_today()
    {
        Assert.Equal("Send the basket today to keep the streak (2 of 3 days). About 4 h left today.", Texts.Warn(new Warning(WarningKind.Streak, 4, "k", 2, 3), false));
    }

    [Fact]
    public void The_to_bag_toast_says_1_stack_when_the_basket_held_one_and_stacks_for_two()
    {
        Assert.Equal("1 stack did not fit in your bag.", Texts.BagShort(1, 1, false));
        Assert.Equal("有 1 组放不进背包。", Texts.BagShort(1, 1, true));
        Assert.Equal("1 of 2 stacks did not fit in your bag.", Texts.BagShort(1, 2, false));
        Assert.Equal("2 of 2 stacks did not fit in your bag.", Texts.BagShort(2, 2, false));
    }

    [Fact]
    public void The_break_down_toast_says_1_gift_when_there_was_one_and_gifts_for_two()
    {
        Assert.Equal("1 gift could not be broken down, because your bag was full or the game did not allow it. If it made it to your bag, it is still there.", Texts.BreakDownShort(1, 1, false));
        Assert.Equal("有 1 件回礼无法拆解，可能是背包满了或游戏不允许。如果已经放进背包，它还在背包里。", Texts.BreakDownShort(1, 1, true));
        Assert.Equal("1 of 2 gifts could not be broken down, because your bag was full or the game did not allow it. Any that made it to your bag are still there.", Texts.BreakDownShort(1, 2, false));
    }
}
