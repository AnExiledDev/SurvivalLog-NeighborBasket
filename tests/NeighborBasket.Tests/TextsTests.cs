using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class TextsTests
{
    private static readonly Dictionary<int, string> Names = new() { [4] = "Tools", [6] = "Cloth" };

    private static Candidate Item(long id, int category) => new(id, 1, 1, category, 5, 1, false);

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
    public void The_fill_toast_names_what_went_in_and_what_could_not_be_found()
    {
        var result = new FillResult(new[] { new Pick(Item(1, 4), true) }, new[] { 6 }, FillStop.None, false);

        Assert.Equal("Put 1 in the basket for: Tools. Nothing at home or in your bag for: Cloth.", Texts.Outcome(result, 0, Names, false));
    }

    [Fact]
    public void Refused_moves_are_counted_apart_from_the_ones_that_went_in()
    {
        var result = new FillResult(new[] { new Pick(Item(1, 4), true), new Pick(Item(2, 6), true) }, new int[0], FillStop.None, false);

        Assert.Equal("Put 1 in the basket for: Tools, Cloth. 1 could not be moved.", Texts.Outcome(result, 1, Names, false));
    }

    [Fact]
    public void An_unnamed_category_shows_its_number()
    {
        var result = new FillResult(new Pick[0], new[] { 12 }, FillStop.None, false);

        Assert.Equal("Nothing at home or in your bag for: #12.", Texts.Outcome(result, 0, Names, false));
    }

    [Fact]
    public void Warnings_carry_the_hours_left()
    {
        Assert.Equal("About 7 h until her condition drops a step. Send the basket to hold it.", Texts.Warn(new Warning(WarningKind.TierDrop, 7, "k"), false));
        Assert.Contains("7", Texts.Warn(new Warning(WarningKind.Struggle, 7, "k"), true));
    }
}
