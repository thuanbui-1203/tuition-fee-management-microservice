using FluentAssertions;
using OtpService.Application.Security;

namespace OtpService.UnitTests;

[Trait("Category", "Unit")]
public class CryptoSecureOtpGeneratorTests
{
    private readonly CryptoSecureOtpGenerator _generator = new();

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Generate_ReturnsExactLengthDigits(int length)
    {
        var otp = _generator.Generate(length);
        otp.Should().HaveLength(length);
        otp.Should().MatchRegex("^[0-9]+$");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void Generate_RejectsInvalidLength(int length)
    {
        var act = () => _generator.Generate(length);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_ProducesVariedValues()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            seen.Add(_generator.Generate(6));
        }

        // Extremely unlikely to produce only a handful of distinct values.
        seen.Count.Should().BeGreaterThan(50);
    }
}
