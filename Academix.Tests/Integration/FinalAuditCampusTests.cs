using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Identity;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed partial class Phase19CompletionWorkflowTests
{
    [Fact]
    public async Task FinalAuditCampusReadsEnforceTenantAndAdministrativePoliciesOverHttp()
    {
        var own=await Setup();var foreign=await Setup();
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var security=scope.ServiceProvider.GetRequiredService<IdentitySecurity>();
        var building=new CampusBuilding{InstitutionId=own.Inst,Code="AUD",Name="Audit building"};
        var occupied=new CampusRoom{InstitutionId=own.Inst,BuildingId=building.Id,Code="O",Name="Occupied",Kind="Classroom",Capacity=30};
        var free=new CampusRoom{InstitutionId=own.Inst,BuildingId=building.Id,Code="F",Name="Free",Kind="Classroom",Capacity=30};
        var from=DateTime.UtcNow.AddDays(2);var until=from.AddHours(2);
        db.AddRange(building,occupied,free,new ClassSession{InstitutionId=own.Inst,SectionId=own.Section,RoomId=occupied.Id,StartsAtUtc=from.AddMinutes(15),EndsAtUtc=from.AddMinutes(45),Topic="Private administrative occupancy"});
        await db.SaveChangesAsync();
        async Task<Guid> NewUser(Guid? institution,string role)
        {
            var mail=Guid.NewGuid().ToString("N")+"@example.test";var user=new ApplicationUser{InstitutionId=institution,Email=mail,UserName=mail};
            Assert.True((await users.CreateAsync(user,"Safe!CampusAudit2026")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);return user.Id;
        }
        var staff=await NewUser(own.Inst,AcademixRoles.Instructor);var foreignStaff=await NewUser(foreign.Inst,AcademixRoles.Instructor);
        var global=await NewUser(null,AcademixRoles.SuperAdministrator);
        var actors=new[]{(own.Student,false,true),(staff,false,true),(own.AdminId,true,true),
            (foreign.Student,false,false),(foreignStaff,false,false),(foreign.AdminId,false,false),(global,true,true)};
        var range="fromUtc="+Uri.EscapeDataString(from.ToString("O"))+"&toUtc="+Uri.EscapeDataString(until.ToString("O"));
        var routes=new[]{($"rooms/{occupied.Id}/occupancy",true), ("calendar",true),("rooms/available-at",false)};
        foreach(var (actor,admin,member) in actors)
        {
            var person=(await users.FindByIdAsync(actor.ToString()))!;
            var token=security.IssueAccessToken(person,(await users.GetRolesAsync(person)).ToArray(),await users.GetSecurityStampAsync(person));
            foreach(var (route,administrative) in routes)
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,$"/api/campus-facilities/institutions/{own.Inst}/{route}?{range}&requiredCapacity=1");
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
                using var response=await fixture.Client.SendAsync(request);
                var allowed=administrative?admin:member;
                Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
                var json=await response.Content.ReadFromJsonAsync<JsonElement>();
                if(allowed)
                {
                    Assert.Equal(JsonValueKind.Array,json.ValueKind);Assert.Equal(1,json.GetArrayLength());
                    Assert.Contains((administrative?own.Section:free.Id).ToString(),json.GetRawText());
                }
                else {Assert.Equal(JsonValueKind.Object,json.ValueKind);Assert.DoesNotContain(own.Section.ToString(),json.GetRawText());}
            }
        }
    }
}
