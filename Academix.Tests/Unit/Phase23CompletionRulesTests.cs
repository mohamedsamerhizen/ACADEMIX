using Academix.Api.DTOs.StudentServices;
using Academix.Api.Services.StudentServices;
using Academix.Api.Middleware;

namespace Academix.Tests.Unit;

public sealed class Phase23CompletionRulesTests
{
    [Theory]
    [InlineData("GeneralSupport")]
    [InlineData("StudentAffairs")]
    [InlineData("ITSupport")]
    [InlineData("Library")]
    [InlineData("CampusServices")]
    public void SupportedServiceDesks(string code) => Assert.True(StudentServiceRules.IsQueue(code));

    [Theory]
    [InlineData("Finance")]
    [InlineData("Graduation")]
    [InlineData("AcademicRequests")]
    [InlineData("")]
    public void NativeWorkflowsCannotBeRecreatedAsGenericQueues(string code)
        => Assert.False(StudentServiceRules.IsQueue(code));

    [Fact]
    public void ReferralRejectsSameQueue()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateReferral(
            new ServiceCaseRefer("Library", 2, "refer"), "Library"));

    [Fact]
    public void ReferralRequiresReasonAndVersion()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateReferral(
            new ServiceCaseRefer("ITSupport", 0, " "), "Library"));

    [Theory]
    [InlineData("Normal")]
    [InlineData("Critical")]
    [InlineData("emergency")]
    public void EscalationRejectsUnsupportedPriority(string priority)
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateEscalation(
            new ServiceCaseEscalate(priority, 1, "reason")));

    [Fact]
    public void EscalationRequiresConcurrencyVersion()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateEscalation(
            new ServiceCaseEscalate("Urgent", 0, "reason")));

    [Fact]
    public void AssignmentRequiresJustification()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAssignment(
            new ServiceCaseAssign(Guid.NewGuid(), 1, "")));

    [Fact]
    public void PdfAttachmentAcceptsMatchingHeader()
        => StudentServiceRules.ValidateAttachment("evidence.pdf", "application/pdf", 7,
            "%PDF-1."u8.ToArray());

    [Fact]
    public void AttachmentRejectsSpoofedMimeType()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAttachment(
            "evidence.pdf", "application/pdf", 5, "HELLO"u8.ToArray()));

    [Fact]
    public void AttachmentRejectsOversizedPayload()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAttachment(
            "evidence.pdf", "application/pdf", 2097153, new byte[2097153]));

    [Fact]
    public void AttachmentRejectsExtensionMismatch()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAttachment(
            "evidence.txt", "application/pdf", 5, "%PDF-"u8.ToArray()));

    [Fact]
    public void StudentViewContainsNoPrivateNoteContract()
    {
        Assert.DoesNotContain(typeof(ServiceCaseEventView).GetProperties(), x =>
            x.Name.Contains("Private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(ServiceAttachmentView).GetProperties(), x => x.Name == "Content");
    }
}
