using NeighborBasket.Core;
using Xunit;

namespace NeighborBasket.Tests;

public class PageCommandTests
{
    [Fact]
    public void The_buttons_and_the_script_messages_parse_to_their_commands()
    {
        Assert.Equal(new PageCommand.Fill(), PageCommand.Parse("slmod|neighborbasket|fill"));
        Assert.Equal(new PageCommand.CallBack(), PageCommand.Parse("slmod|neighborbasket|callback"));
        Assert.Equal(new PageCommand.Ready(), PageCommand.Parse("slmod|neighborbasket|ready"));
        Assert.Equal(new PageCommand.NoScript(), PageCommand.Parse("slmod|neighborbasket|noscript"));
        Assert.Equal(new PageCommand.PutAway(), PageCommand.Parse("slmod|neighborbasket|putaway"));
        Assert.Equal(new PageCommand.App(), PageCommand.Parse("slmod|neighborbasket|app"));
        Assert.Equal(new PageCommand.Station(), PageCommand.Parse("slmod|neighborbasket|station"));
        Assert.Equal(new PageCommand.Add(42, false), PageCommand.Parse("slmod|neighborbasket|add|42|one"));
        Assert.Equal(new PageCommand.Add(42, true), PageCommand.Parse("slmod|neighborbasket|add|42|all"));
        Assert.Equal(new PageCommand.Take(42), PageCommand.Parse("slmod|neighborbasket|take|42"));
    }

    [Theory]
    [InlineData("slmod|neighborbasket|fill|now")]
    [InlineData("slmod|neighborbasket|diag|cannot read the basket panel state")]
    [InlineData("slmod|neighborbasket|")]
    [InlineData("slmod|neighborbasket|card")]
    [InlineData("slmod|neighborbasket|add|42|two")]
    [InlineData("slmod|neighborbasket|add|-42|one")]
    [InlineData("slmod|neighborbasket|add|0|one")]
    [InlineData("slmod|neighborbasket|take|x")]
    public void Anything_else_with_our_prefix_is_unknown_so_it_is_logged_not_acted_on(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void Other_mods_messages_are_not_ours()
    {
        Assert.False(PageCommand.IsOurs("slmod|unpackall|unpack"));
        Assert.False(PageCommand.IsOurs(null));
    }

    [Fact]
    public void A_js_literal_keeps_quotes_and_script_tags_inert()
    {
        Assert.Equal("\"a\\\"b\\u003c/script>\"", JsText.Literal("a\"b</script>"));
    }
}
