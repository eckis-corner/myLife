using MyLife.Shared;

namespace MyLife.Tests;

public sealed class SupportedCulturesTests
{
    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("en-US", "en")]
    [InlineData("EN", "en")]
    [InlineData("fr-FR", "de")]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    public void ResolveFallsBackToGerman(string? input, string expected) =>
        Assert.Equal(expected, SupportedCultures.Resolve(input));
}
