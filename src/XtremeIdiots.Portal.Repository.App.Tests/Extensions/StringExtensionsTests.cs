using XtremeIdiots.Portal.Repository.App.Extensions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Extensions;

public class StringExtensionsTests
{
    [Fact]
    public void NormalizeName_WithSimpleName_ReturnsUppercased()
    {
        var result = "PlayerOne".NormalizeName();
        Assert.Equal("PLAYERONE", result);
    }

    [Fact]
    public void NormalizeName_WithColorCodes_RemovesColorCodes()
    {
        var result = "^1Player^2One".NormalizeName();
        Assert.Equal("PLAYERONE", result);
    }

    [Fact]
    public void NormalizeName_WithClanTag_RemovesClanTag()
    {
        var result = "[TAG]PlayerOne".NormalizeName();
        Assert.Equal("PLAYERONE", result);
    }

    [Fact]
    public void NormalizeName_WithColorCodesAndClanTag_RemovesBoth()
    {
        var result = "^1[TAG]^2PlayerOne".NormalizeName();
        Assert.Equal("PLAYERONE", result);
    }

    [Fact]
    public void NormalizeName_WithEmptyString_ReturnsEmpty()
    {
        var result = "".NormalizeName();
        Assert.Equal("", result);
    }

    [Fact]
    public void NormalizeName_WithAllColorCodes_RemovesAll()
    {
        var result = "^0^1^2^3^4^5^6^7^8^9Test".NormalizeName();
        Assert.Equal("TEST", result);
    }

    [Fact]
    public void NormalizeName_WithWhitespace_TrimsResult()
    {
        var result = "  Player  ".NormalizeName();
        Assert.Equal("PLAYER", result);
    }

    [Fact]
    public void NormalizeName_WithNonStartingBrackets_DoesNotRemoveTag()
    {
        var result = "Player[TAG]".NormalizeName();
        Assert.Equal("PLAYER[TAG]", result);
    }

    [Theory]
    [InlineData("^0Red", "RED")]
    [InlineData("^9Blue", "BLUE")]
    [InlineData("^5Mid", "MID")]
    public void NormalizeName_WithIndividualColorCodes_RemovesCorrectly(string input, string expected)
    {
        var result = input.NormalizeName();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeName_WithGreedyBracketMatch_RemovesAllBracketedContent()
    {
        // The regex ^(\[.*\]) is greedy, so [A][B] matches the whole [A][B]
        var result = "[A][B]Player".NormalizeName();
        Assert.Equal("PLAYER", result);
    }

    [Fact]
    public void NormalizeName_WithColorCodesInsideClanTag_RemovesColorCodesFirst()
    {
        // Color codes are removed first, then clan tag
        var result = "[^1TAG]Player".NormalizeName();
        Assert.Equal("PLAYER", result);
    }

    [Fact]
    public void NormalizeName_WithOnlyColorCodes_ReturnsEmpty()
    {
        var result = "^1^2^3".NormalizeName();
        Assert.Equal("", result);
    }
}
