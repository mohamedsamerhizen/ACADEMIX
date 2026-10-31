using Academix.Api.Middleware;
using Academix.Api.Services.Institutional;

namespace Academix.Tests.Unit;

public sealed class InstitutionalDocumentPolicyTests
{
    [Theory]
    [InlineData("Enrollment")]
    [InlineData("Transcript")]
    [InlineData("Standing")]
    [InlineData("Graduation")]
    public void SupportedKinds(string kind) => Assert.Equal(kind, InstitutionalDocumentPolicyRules.Kind(kind));

    [Theory]
    [InlineData("FinancialStatement")]
    [InlineData("")]
    public void RejectsUnknownKinds(string kind) => Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.Kind(kind));

    [Fact]
    public void PurposeAndReasonAreNormalizedAndValidated()
    {
        Assert.Equal("For employer", InstitutionalDocumentPolicyRules.Purpose(" For employer "));
        Assert.Equal("Not eligible", InstitutionalDocumentPolicyRules.Reason(" Not eligible "));
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.Purpose(" "));
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.Reason(new string('x', 501)));
    }

    [Fact]
    public void RestrictsValidityAndStaleWrites()
    {
        Assert.Equal(180, InstitutionalDocumentPolicyRules.ValidityDays(180));
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.ValidityDays(0));
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.ValidityDays(366));
        InstitutionalDocumentPolicyRules.EnsureVersion(2, 2);
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.EnsureVersion(3, 2));
    }

    [Fact]
    public void ChecksStateTransitionsAndSelfReview()
    {
        InstitutionalDocumentPolicyRules.EnsureTransition("Submitted", "Submitted");
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.EnsureTransition("Issued", "Submitted"));
        var actor = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => InstitutionalDocumentPolicyRules.EnsureIndependentReviewer(actor, actor));
    }

    [Fact]
    public void RevocationAlwaysOverridesExpiry()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Valid", InstitutionalDocumentPolicyRules.State(false, now.AddDays(1), now));
        Assert.Equal("Expired", InstitutionalDocumentPolicyRules.State(false, now, now));
        Assert.Equal("Revoked", InstitutionalDocumentPolicyRules.State(true, now.AddDays(1), now));
        Assert.False(InstitutionalDocumentPolicyRules.Allowed("Transcript", true, false, true));
    }
}
