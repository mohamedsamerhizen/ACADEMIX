using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.StudentServices;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Courses;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Learning;
using Academix.Api.Services.Examinations;
using Academix.Api.Services.Workspaces;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.AdvancedLearning;
using Academix.Api.Services.Assessment;
using Academix.Api.Services.Communications;
using Academix.Api.Services.Governance;
using Academix.Api.Services.Analytics;
using Academix.Api.Services.Quality;
using Academix.Api.Services.Advising;
using Academix.Api.Services.Records;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Lifecycle;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

namespace Academix.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAcademixServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AcademixDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Default.");
            options.UseNpgsql(connection, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        }).AddRoles<ApplicationRole>()
          .AddSignInManager()
          .AddEntityFrameworkStores<AcademixDbContext>()
          .AddDefaultTokenProviders();

        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey requires at least 32 bytes.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "ACADEMIX",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "ACADEMIX.Api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
        });

        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        services.AddScoped<Academix.Api.Services.Registrar.AcademicPolicyEngine>();
        services.AddScoped<Academix.Api.Services.Registrar.AcademicPolicyService>();
        services.AddScoped<Academix.Api.Services.Registrar.InstitutionalImportService>();
        var dataProtection=services.AddDataProtection().SetApplicationName("ACADEMIX");
        if(configuration["DataProtection:KeyRingPath"] is string keyRing && !string.IsNullOrWhiteSpace(keyRing))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRing));
        services.AddScoped<Academix.Api.Services.Registrar.ApplicantService>();
        services.AddScoped<DocumentKeyService>();
        services.AddScoped<DocumentArtifactCleanup>();
        services.AddScoped<LearningArtifactCleanup>();
        services.AddScoped<Academix.Api.Services.Advising.AcademicProgressAccess>();
        services.AddScoped<Academix.Api.Services.Curriculum.DegreeAuditService>();
        services.AddScoped<Academix.Api.Services.Finance.FinanceAccess>();
        services.AddScoped<Academix.Api.Services.Finance.FinanceService>();
        services.AddSingleton<DocumentPdfRenderer>();
        services.AddScoped<OutboxService>();
        services.AddHostedService<LegacyDocumentProofInitializer>();
        services.AddScoped<RetryingServiceWork>();
        services.AddScoped<ExamExpirationService>();
        services.AddHostedService<AcademicBackgroundWorker>();
        services.AddScoped<IdentitySecurity>();
        services.AddScoped<AuthService>();
        services.AddScoped<StructureService>();
        services.AddScoped<AcademicOperations>();
        services.AddScoped<LearningOperations>();
        services.AddScoped<QuestionBankService>();
        services.AddScoped<ExamBuilderService>();
        services.AddScoped<ExamTakingService>();
        services.AddScoped<ExamGradingService>();
        services.AddScoped<StudentWorkspaceService>();
        services.AddScoped<InstructorWorkspaceService>();
        services.AddScoped<InstitutionWorkspaceService>();
        services.AddScoped<PlatformGovernanceService>();
        services.AddScoped<TeachingOperationsService>();
        services.AddScoped<CampusFacilitiesService>();
        services.AddScoped<StudentServiceCaseService>();
        services.AddScoped<CampusReservationService>();
        services.AddScoped<StudentAcademicOperationsService>();
        services.AddScoped<RegistrarOperationsService>();
        services.AddScoped<AdvancedLearningAccess>();
        services.AddScoped<DiscussionService>();
        services.AddScoped<GroupLearningService>();
        services.AddScoped<GradebookService>();
        services.AddScoped<CommunicationService>();
        services.AddScoped<GovernanceService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<QualityService>();
        services.AddScoped<QualityEvidenceService>();
        services.AddScoped<AdvisingService>();
        services.AddScoped<RecordsService>();
        services.AddScoped<CurriculumService>();
        services.AddScoped<LifecycleService>();
        services.AddScoped<InstitutionalDocumentService>();
        services.AddSingleton<PrivateFileStore>();
        services.AddSingleton<IPrivateFileStore>(p=>p.GetRequiredService<PrivateFileStore>());
        services.AddScoped<ICourseQueries, CourseQueries>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddControllers(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        services.AddOpenApi();
        services.AddHealthChecks().AddDbContextCheck<AcademixDbContext>(
            "postgresql", failureStatus: HealthStatus.Unhealthy);

        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Invalid identity.");
            return;
        }

        var idText = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idText, out var userId))
        {
            context.Fail("Invalid identity.");
            return;
        }

        var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var db = context.HttpContext.RequestServices.GetRequiredService<AcademixDbContext>();
        var user = await manager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive ||
            principal.FindFirstValue("security_stamp") != await manager.GetSecurityStampAsync(user))
        {
            context.Fail("Session revoked.");
            return;
        }

        if (principal.FindFirstValue("institution_id") != user.InstitutionId?.ToString())
        {
            context.Fail("Institution membership changed.");
            return;
        }

        var roles = await manager.GetRolesAsync(user);
        if (!roles.Contains(AcademixRoles.SuperAdministrator) &&
            (user.InstitutionId is not Guid institutionId ||
             !await db.Institutions.AnyAsync(x => x.Id == institutionId && x.IsActive)))
        {
            context.Fail("Institution disabled.");
        }
    }
}
