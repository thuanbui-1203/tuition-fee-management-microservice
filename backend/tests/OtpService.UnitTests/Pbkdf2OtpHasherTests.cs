using FluentAssertions;
using OtpService.Application.Security;

namespace OtpService.UnitTests;

[Trait("Category", "Unit")]
public class Pbkdf2OtpHasherTests
{
    private readonly Pbkdf2OtpHasher _hasher = new();

    [Fact]
    public void Hash_Then_Verify_ReturnsTrue()
    {
        var hash = _hasher.Hash("482913");
        _hasher.Verify("482913", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongCode_ReturnsFalse()
    {
        var hash = _hasher.Hash("482913");
        _hasher.Verify("999999", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SameCodeTwice_ProducesDifferentHashes()
    {
        var a = _hasher.Hash("482913");
        var b = _hasher.Hash("482913");
        a.Should().NotBe(b); // fresh salt per record
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        _hasher.Verify("482913", "not-a-hash").Should().BeFalse();
    }
}
