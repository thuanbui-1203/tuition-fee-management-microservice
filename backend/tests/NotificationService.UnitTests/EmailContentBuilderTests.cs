using System.Text.Json;
using FluentAssertions;
using Microservices.Common.Errors;
using Microservices.Common.Messaging;
using Microservices.Common.Security;
using Microsoft.Extensions.Options;
using NotificationService.Application.Services;

namespace NotificationService.UnitTests;

[Trait("Category", "Unit")]
public class EmailContentBuilderTests
{
    private const string TestKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly EmailContentBuilder _builder =
        new(Options.Create(new EventEncryptionOptions { Key = TestKey }));

    [Fact]
    public void Build_OtpRequested_DecryptsAndEmbedsOtp()
    {
        var encrypted = AesGcmCrypto.EncryptToString("482913", Convert.FromBase64String(TestKey));
        var payload = JsonDocument.Parse($"{{\"otp\":\"{encrypted}\",\"expiresInSeconds\":300}}").RootElement;

        var result = _builder.Build(EmailEventTypes.OtpRequested, payload);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subject.Should().Contain("OTP");
        result.Value.HtmlBody.Should().Contain("482913");
        result.Value.TextBody.Should().Contain("482913");
    }

    [Fact]
    public void Build_PaymentSucceeded_FormatsConfirmation()
    {
        var payload = JsonDocument.Parse("{\"studentName\":\"Trần Thị B\",\"mssv\":\"521H0001\",\"semester\":\"2024-2025/HK1\",\"amount\":8400000}").RootElement;

        var result = _builder.Build(EmailEventTypes.PaymentSucceeded, payload);

        result.IsSuccess.Should().BeTrue();
        result.Value.HtmlBody.Should().Contain("8,400,000");
        result.Value.HtmlBody.Should().Contain("521H0001");
    }

    [Fact]
    public void Build_UnsupportedEvent_ReturnsInvalidInput()
    {
        var result = _builder.Build("UnknownEvent", JsonDocument.Parse("{}").RootElement);
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }

    [Fact]
    public void Build_OtpRequested_WrongKey_ReturnsInvalidInput()
    {
        var wrongKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
        var encrypted = AesGcmCrypto.EncryptToString("482913", Convert.FromBase64String(wrongKey));
        var payload = JsonDocument.Parse($"{{\"otp\":\"{encrypted}\",\"expiresInSeconds\":300}}").RootElement;

        var result = _builder.Build(EmailEventTypes.OtpRequested, payload);
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }
}
