using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Registrar;
using Academix.Api.Services.Examinations;

namespace Academix.Tests.Unit;

public sealed class Phase19PolicyAndImportTests
{
    [Theory]
    [InlineData(null, "password")]
    [InlineData("", "password")]
    [InlineData("invalid", "password")]
    [InlineData("a@example.test", null)]
    [InlineData("a@example.test", "")]
    public void InvalidAuthenticationInputFailsAsUnauthorized(string? email, string? password)
        => Assert.Equal(401, Assert.Throws<ApiFault>(() => IdentityInputPolicy.Login(new(email!, password!))).StatusCode);

    [Fact]
    public void NullLoginAndProvisionAreControlledFailures()
    {
        Assert.Equal(401, Assert.Throws<ApiFault>(() => IdentityInputPolicy.Login(null)).StatusCode);
        Assert.Equal(400, Assert.Throws<ApiFault>(() => IdentityInputPolicy.Provision(null)).StatusCode);
        IdentityInputPolicy.Login(new(" student@example.test ", "accepted-by-input-validation"));
    }

    [Theory]
    [InlineData("Submitted", true)]
    [InlineData("Expired", true)]
    [InlineData("InProgress", false)]
    [InlineData("Unknown", false)]
    public void OnlyFinalizedAttemptsCanBeGraded(string status, bool expected)
        => Assert.Equal(expected, ExamAttemptPolicy.CanGrade(status));

    [Fact]
    public void DeadlineIsInclusiveAndSubmittedAttemptsRemainFinal()
    {
        var deadline = DateTime.UtcNow;
        Assert.False(ExamAttemptPolicy.IsExpired("InProgress", deadline, deadline.AddTicks(-1)));
        Assert.True(ExamAttemptPolicy.IsExpired("InProgress", deadline, deadline));
        Assert.False(ExamAttemptPolicy.IsExpired("Submitted", deadline, deadline.AddDays(1)));
    }

    [Theory]
    [InlineData(true, false, false, 12, 3, "Prerequisites")]
    [InlineData(true, true, true, 12, 3, "ActiveAcademicHold")]
    [InlineData(true, true, false, 16, 3, "CreditLoad")]
    public void EligibilityExplainsSpecificRejections(bool enforce, bool prerequisites, bool hold, int current, int requested, string reason)
    {
        var result = AcademicPolicyRulesValidator.Evaluate(new(enforce, true, 18, false), prerequisites, hold, current, requested, new HashSet<string>());
        Assert.Contains(reason, result);
    }

    [Fact]
    public void CreditBoundaryAndScopedExceptionPreserveOtherRules()
    {
        var rules = new AcademicPolicyRules(true, true, 18, true);
        Assert.Equal(new[] { "EnrollmentApproval" }, AcademicPolicyRulesValidator.Evaluate(rules, true, false, 15, 3, new HashSet<string>()));
        Assert.Empty(AcademicPolicyRulesValidator.Evaluate(rules, true, false, 15, 3, new HashSet<string> { "EnrollmentApproval" }));
        Assert.Equal(new[] { "ActiveAcademicHold", "EnrollmentApproval" }, AcademicPolicyRulesValidator.Evaluate(rules, false, true, 20, 3,
            new HashSet<string> { "Prerequisites", "CreditLoad" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void PolicyRejectsUnsafeCreditLimits(int limit)
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ApiFault>(() => AcademicPolicyRulesValidator.Validate(new(null, now, now.AddDays(1), new(true, true, limit, false))));
    }

    [Fact]
    public void PolicyRejectsEmptyAndNonUtcEffectiveIntervals()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ApiFault>(() => AcademicPolicyRulesValidator.Validate(null));
        Assert.Throws<ApiFault>(() => AcademicPolicyRulesValidator.Validate(new(null, now, now, new(true, true, 18, false))));
        Assert.Throws<ApiFault>(() => AcademicPolicyRulesValidator.Validate(new(null, DateTime.SpecifyKind(now, DateTimeKind.Unspecified), now.AddDays(1), new(true, true, 18, false))));
    }

    private static ImportUpload File() => new(Guid.NewGuid(), "LegacySIS", new("external", "mail", "program", "term", "plan"),
        new[] { new Dictionary<string, string?> { ["external"] = "S-100", ["mail"] = "student@example.test", ["program"] = Guid.NewGuid().ToString(),
            ["term"] = Guid.NewGuid().ToString(), ["plan"] = Guid.NewGuid().ToString() } });

    [Fact]
    public void ImportMapsFieldsAndNormalizesIdentityWithoutCredentials()
    {
        var file = File(); var record = Assert.Single(ImportValidation.Parse(file));
        Assert.Equal("S-100", record.ExternalId); Assert.Equal("STUDENT@EXAMPLE.TEST", record.Email);
        Assert.Equal(Guid.Parse(file.Rows[0]["plan"]!), record.PlanId);
    }

    [Fact]
    public void ImportRejectsDuplicateRecordsAndAmbiguousMapping()
    {
        var file = File();
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(file with { Rows = new[] { file.Rows[0], file.Rows[0] } }));
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(file with { Fields = file.Fields with { Email = "external" } }));
    }

    [Theory]
    [InlineData("external", "")]
    [InlineData("mail", "invalid")]
    [InlineData("program", "invalid")]
    [InlineData("term", "00000000-0000-0000-0000-000000000000")]
    public void ImportRejectsMalformedMappedValues(string field, string value)
    {
        var file = File(); file.Rows[0][field] = value;
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(file));
    }

    [Fact]
    public void ImportRejectsExtraFieldsAndUnboundedFiles()
    {
        var file = File(); file.Rows[0]["password"] = "must-not-be-stored";
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(file));
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(File() with { Rows = [] }));
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(File() with { ClientRequestId = Guid.Empty }));
        Assert.Throws<ApiFault>(() => ImportValidation.Parse(null));
    }
}
