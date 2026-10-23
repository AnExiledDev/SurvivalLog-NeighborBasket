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
    }

    [Theory]
    [InlineData("slmod|neighborbasket|fill|now")]
    [InlineData("slmod|neighborbasket|diag|cannot read the basket panel state")]
    [InlineData("slmod|neighborbasket|")]
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
