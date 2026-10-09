using FluentAssertions;
using TuitionService.Application.Validation;

namespace TuitionService.UnitTests;

[Trait("Category", "Unit")]
public class MssvValidatorTests
{
    [Theory]
    [InlineData("521H0001")]
    [InlineData("ABC123")]
    [InlineData("0001")]
    [InlineData("a")]
    public void IsValid_AcceptsAlphanumericMssv(string mssv)
    {
        MssvValidator.IsValid(mssv).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_RejectsEmptyMssv(string? mssv)
    {
        MssvValidator.IsValid(mssv).Should().BeFalse();
    }

    [Theory]
    [InlineData("521H-0001")]
    [InlineData("521H 0001")]
    [InlineData("521H0001@")]
    [InlineData("521H0001!")]
    public void IsValid_RejectsNonAlphanumeric(string mssv)
    {
        MssvValidator.IsValid(mssv).Should().BeFalse();
    }

    [Fact]
    public void IsValid_RejectsOversizedMssv()
    {
        MssvValidator.IsValid(new string('A', MssvValidator.MaxLength + 1)).Should().BeFalse();
    }
}
