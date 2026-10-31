using Academix.Api.Controllers;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
namespace Academix.Tests.Security;

public sealed class Phase21FinanceSecurityTests
{
    [Theory][InlineData(AcademixRoles.Student)][InlineData(AcademixRoles.Instructor)][InlineData(AcademixRoles.TeachingAssistant)]
    public void FinancialGrantCannotOverrideCurrentInstitutionalRole(string role)
    {var inst=Guid.NewGuid();Assert.False(FinanceAccess.Allowed(inst,[role],inst,(FinancePermission)255,FinancePermission.Payments));}
    [Fact] public void AdministratorStillNeedsExplicitFinancePermission()
    {var inst=Guid.NewGuid();Assert.False(FinanceAccess.Allowed(inst,[AcademixRoles.InstitutionAdministrator],inst,FinancePermission.Read,FinancePermission.Payments));Assert.False(FinanceAccess.Allowed(inst,[AcademixRoles.InstitutionAdministrator],inst,FinancePermission.None,FinancePermission.None));Assert.True(FinanceAccess.Allowed(inst,[AcademixRoles.InstitutionAdministrator],inst,FinancePermission.Payments,FinancePermission.Payments));}
    [Fact] public void CrossInstitutionFinancialGrantsDoNotConferAccess()
    {Assert.False(FinanceAccess.Allowed(Guid.NewGuid(),[AcademixRoles.InstitutionAdministrator],Guid.NewGuid(),(FinancePermission)255,FinancePermission.Report));}
    [Fact] public void RoleRevocationInvalidatesPreviouslyGrantedFinanceAuthority()
    {var inst=Guid.NewGuid();Assert.True(FinanceAccess.Allowed(inst,[AcademixRoles.InstitutionAdministrator],inst,FinancePermission.Approve,FinancePermission.Approve));Assert.False(FinanceAccess.Allowed(inst,[],inst,FinancePermission.Approve,FinancePermission.Approve));}
    [Fact] public void StudentFinancialVisibilityRequiresOwnIdentityAndInstitution()
    {var account=new FinancialAccount{InstitutionId=Guid.NewGuid(),StudentId=Guid.NewGuid(),Currency="USD"};Assert.True(FinanceAccess.OwnAccount(account.StudentId,account.InstitutionId,[AcademixRoles.Student],account));Assert.False(FinanceAccess.OwnAccount(Guid.NewGuid(),account.InstitutionId,[AcademixRoles.Student],account));Assert.False(FinanceAccess.OwnAccount(account.StudentId,Guid.NewGuid(),[AcademixRoles.Student],account));}
    [Fact] public void WaiverAndRefundApprovalsCannotBeSelfGranted()
    {var student=Guid.NewGuid();var requester=Guid.NewGuid();Assert.Throws<ApiFault>(()=>FinanceAccess.Independent(student,student,requester));Assert.Throws<ApiFault>(()=>FinanceAccess.Independent(requester,student,requester));FinanceAccess.Independent(Guid.NewGuid(),student,requester);}
    [Fact] public void OnlyProofVerificationIsAnonymousOnFinanceController()
    {Assert.NotEmpty(typeof(FinanceController).GetCustomAttributes(typeof(AuthorizeAttribute),true));Assert.Equal(new[]{nameof(FinanceController.Verify)},typeof(FinanceController).GetMethods().Where(x=>x.DeclaringType==typeof(FinanceController)&&x.IsDefined(typeof(AllowAnonymousAttribute),true)).Select(x=>x.Name));}
    [Fact] public void AssessmentRequestsCannotSupplyAnAuthoritativeTuitionTotal()
    {Assert.DoesNotContain(typeof(AssessmentInput).GetProperties(),x=>x.Name.Contains("Amount",StringComparison.Ordinal)||x.Name.Contains("Total",StringComparison.Ordinal));}
    [Fact] public void PaymentContractsDoNotAcceptCardCredentials()
    {
        Assert.DoesNotContain(typeof(PaymentInput).GetProperties(),x=>new[]{"CardNumber","Cvv","Pan","Expiry","Pin"}.Contains(x.Name));
        Assert.Throws<System.Text.Json.JsonException>(()=>System.Text.Json.JsonSerializer.Deserialize<PaymentInput>("{\"Currency\":\"USD\",\"Method\":\"BankTransfer\",\"Reference\":\"BANK-1\",\"AmountMinor\":100,\"CardNumber\":\"never-accept\"}"));
    }
}
