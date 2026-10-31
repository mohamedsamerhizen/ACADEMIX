using Academix.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Academix.Tests.Security;

public sealed class ControllerAuthorizationTests
{
    private static readonly Type[] ProtectedControllers =
    [
        typeof(QualityController), typeof(FinanceController),
        typeof(ApplicantOnboardingController), typeof(RegistrarCompletionController), typeof(AcademicPoliciesController), typeof(InstitutionalImportsController), typeof(CommunicationsController), typeof(UsersController), typeof(InstitutionsController), typeof(FacultiesController),
        typeof(DepartmentsController), typeof(ProgramsController), typeof(AcademicTermsController),
        typeof(CoursesController), typeof(SectionsController), typeof(EnrollmentsController),
        typeof(LearningUnitsController), typeof(LessonsController), typeof(AnnouncementsController),
        typeof(AssignmentsController), typeof(SubmissionsController), typeof(LearningFilesController),
        typeof(QuestionBankController), typeof(ExamBuilderController), typeof(ExamTakingController),
        typeof(ExamGradingController), typeof(ExamResultsController),
        typeof(StudentWorkspaceController), typeof(InstructorWorkspaceController),
        typeof(InstitutionWorkspaceController), typeof(PlatformGovernanceController),
        typeof(AcademicTeachingOperationsController), typeof(StudentAcademicOperationsController),
        typeof(InstitutionAcademicOperationsController)
    ];

    [Fact]
    public void ProtectedControllersRequireAuthorization()
    {
        Assert.All(ProtectedControllers, controller =>
            Assert.NotEmpty(controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)));
    }

    [Fact]
    public void OnlyLoginAndRefreshAreAnonymousIdentityActions()
    {
        var anonymous = typeof(AuthController).GetMethods()
            .Where(method => method.DeclaringType == typeof(AuthController))
            .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Equal([nameof(AuthController.Login), nameof(AuthController.Refresh)], anonymous);
        Assert.True(typeof(AuthController).GetMethod(nameof(AuthController.Logout))!
            .IsDefined(typeof(AuthorizeAttribute), inherit: true));
        Assert.True(typeof(AuthController).GetMethod(nameof(AuthController.Me))!
            .IsDefined(typeof(AuthorizeAttribute), inherit: true));
    }

    [Theory]
    [InlineData(nameof(AuthController.Login))]
    [InlineData(nameof(AuthController.Refresh))]
    public void CredentialIssuingActionsRemainRateLimited(string methodName)
    {
        var method = typeof(AuthController).GetMethod(methodName)!;
        var attribute = Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>());
        Assert.Equal("authentication", attribute.PolicyName);
    }

    [Fact]
    public void PublicCatalogIsExplicitlyAnonymous()
    {
        Assert.NotEmpty(typeof(CourseCatalogController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }
}
