using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Complete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_courses_code",
                table: "courses");

            

            

            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                table: "courses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("d63857d2-dbb5-4659-a879-a8a8d0f53101"));

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "courses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_courses_id_institution_id",
                table: "courses",
                columns: new[] { "id", "institution_id" });

            migrationBuilder.CreateTable(
                name: "identity_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Target = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_identity_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "identity_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academic_terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_terms", x => x.Id);
                    table.UniqueConstraint("AK_academic_terms_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_academic_terms_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "faculties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculties", x => x.Id);
                    table.UniqueConstraint("AK_faculties_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_faculties_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_users_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_sections", x => x.Id);
                    table.UniqueConstraint("AK_course_sections_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_course_sections_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_course_sections_courses_CourseId_InstitutionId",
                        columns: x => new { x.CourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                    table.UniqueConstraint("AK_departments_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_departments_faculties_FacultyId_InstitutionId",
                        columns: x => new { x.FacultyId, x.InstitutionId },
                        principalTable: "faculties",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_departments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_identity_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "identity_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_refresh_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_refresh_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_refresh_sessions_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnrolledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_enrollments_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_enrollments_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teaching_assignments",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teaching_assignments", x => new { x.SectionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_teaching_assignments_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_teaching_assignments_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "academic_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_programs", x => x.Id);
                    table.UniqueConstraint("AK_academic_programs_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_academic_programs_departments_DepartmentId_InstitutionId",
                        columns: x => new { x.DepartmentId, x.InstitutionId },
                        principalTable: "departments",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_courses",
                columns: table => new
                {
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_courses", x => new { x.ProgramId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_program_courses_academic_programs_ProgramId_InstitutionId",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_courses_courses_CourseId_InstitutionId",
                        columns: x => new { x.CourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            

            migrationBuilder.CreateIndex(
                name: "ux_courses_institution_code",
                table: "courses",
                columns: new[] { "institution_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_programs_DepartmentId_InstitutionId",
                table: "academic_programs",
                columns: new[] { "DepartmentId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_programs_InstitutionId_Code",
                table: "academic_programs",
                columns: new[] { "InstitutionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_terms_InstitutionId_Code",
                table: "academic_terms",
                columns: new[] { "InstitutionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_course_sections_CourseId_InstitutionId",
                table: "course_sections",
                columns: new[] { "CourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_course_sections_InstitutionId_CourseId_TermId_Code",
                table: "course_sections",
                columns: new[] { "InstitutionId", "CourseId", "TermId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_course_sections_TermId_InstitutionId",
                table: "course_sections",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_departments_FacultyId_InstitutionId",
                table: "departments",
                columns: new[] { "FacultyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_departments_InstitutionId_Code",
                table: "departments",
                columns: new[] { "InstitutionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faculties_InstitutionId_Code",
                table: "faculties",
                columns: new[] { "InstitutionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_refresh_sessions_TokenHash",
                table: "identity_refresh_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_refresh_sessions_UserId_FamilyId",
                table: "identity_refresh_sessions",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "identity_roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "identity_users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_users_InstitutionId",
                table: "identity_users",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "identity_users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutions_Code",
                table: "institutions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_courses_CourseId_InstitutionId",
                table: "program_courses",
                columns: new[] { "CourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_program_courses_ProgramId_InstitutionId",
                table: "program_courses",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_AtUtc",
                table: "security_audit_events",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_SectionId_InstitutionId",
                table: "student_enrollments",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_SectionId_StudentId",
                table: "student_enrollments",
                columns: new[] { "SectionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_StudentId_Status",
                table: "student_enrollments",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_teaching_assignments_SectionId_InstitutionId",
                table: "teaching_assignments",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_teaching_assignments_SectionId_Kind",
                table: "teaching_assignments",
                columns: new[] { "SectionId", "Kind" },
                unique: true,
                filter: "\"Kind\" = 'Instructor'");

            migrationBuilder.CreateIndex(
                name: "IX_teaching_assignments_UserId",
                table: "teaching_assignments",
                column: "UserId");

                        migrationBuilder.Sql("INSERT INTO institutions (\"Id\",\"Code\",\"Name\",\"IsActive\",\"Version\") VALUES ('d63857d2-dbb5-4659-a879-a8a8d0f53101','ACADEMIX','Academix Initial Institution',TRUE,1) ON CONFLICT (\"Id\") DO NOTHING;");
migrationBuilder.AddForeignKey(
                name: "FK_courses_institutions_institution_id",
                table: "courses",
                column: "institution_id",
                principalTable: "institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        
        // ACADEMIX_PHASE1_PRESERVE_SEEDED_COURSE_VERSIONS
        migrationBuilder.Sql("UPDATE courses SET version = 1 WHERE version = 0 AND id IN ('8b217247-2c98-4b79-ac2e-bbe086a2ad11', 'fe9ed719-2e94-498a-988c-4c8e2450c2d2');");
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_courses_institutions_institution_id",
                table: "courses");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "identity_refresh_sessions");

            migrationBuilder.DropTable(
                name: "program_courses");

            migrationBuilder.DropTable(
                name: "security_audit_events");

            migrationBuilder.DropTable(
                name: "student_enrollments");

            migrationBuilder.DropTable(
                name: "teaching_assignments");

            migrationBuilder.DropTable(
                name: "identity_roles");

            migrationBuilder.DropTable(
                name: "academic_programs");

            migrationBuilder.DropTable(
                name: "course_sections");

            migrationBuilder.DropTable(
                name: "identity_users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "academic_terms");

            migrationBuilder.DropTable(
                name: "faculties");

            migrationBuilder.DropTable(
                name: "institutions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_courses_id_institution_id",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "ux_courses_institution_code",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "institution_id",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "version",
                table: "courses");

            migrationBuilder.CreateIndex(
                name: "ux_courses_code",
                table: "courses",
                column: "code",
                unique: true);
        }
    }
}
