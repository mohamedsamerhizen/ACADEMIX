using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase21StudentFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_accounts", x => x.Id);
                    table.UniqueConstraint("AK_finance_accounts_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_finance_accounts_Id_InstitutionId_Currency", x => new { x.Id, x.InstitutionId, x.Currency });
                    table.CheckConstraint("CK_finance_accounts_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_account", "\"Status\" IN ('Active','Frozen')");
                    table.ForeignKey(
                        name: "FK_finance_accounts_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_accounts_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_authorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_authorities", x => x.Id);
                    table.UniqueConstraint("AK_finance_authorities_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_authority", "\"Permissions\" BETWEEN 0 AND 255");
                    table.ForeignKey(
                        name: "FK_finance_authorities_identity_users_UserId",
                        column: x => x.UserId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_authorities_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_commands", x => x.Id);
                    table.UniqueConstraint("AK_finance_commands_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_finance_commands_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_fee_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYear = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_fee_schedules", x => x.Id);
                    table.UniqueConstraint("AK_finance_fee_schedules_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_finance_fee_schedules_Id_InstitutionId_Currency", x => new { x.Id, x.InstitutionId, x.Currency });
                    table.CheckConstraint("CK_finance_fee_schedules_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_schedule", "\"Status\" IN ('Draft','Published','Retired') AND \"Revision\" > 0 AND \"EffectiveFrom\" < \"EffectiveUntil\" AND ((\"TermId\" IS NOT NULL AND \"AcademicYear\" IS NULL) OR (\"TermId\" IS NULL AND \"AcademicYear\" BETWEEN 2000 AND 2200))");
                    table.ForeignKey(
                        name: "FK_finance_fee_schedules_academic_programs_ProgramId_Instituti~",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_fee_schedules_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_fee_schedules_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequirePaymentVerification = table.Column<bool>(type: "boolean", nullable: false),
                    AllowRefunds = table.Column<bool>(type: "boolean", nullable: false),
                    BlockRefundWithDebt = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumInstallments = table.Column<int>(type: "integer", nullable: false),
                    MaximumExtensionDays = table.Column<int>(type: "integer", nullable: false),
                    RequirePlanApproval = table.Column<bool>(type: "boolean", nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    OverdueThresholdMinor = table.Column<long>(type: "bigint", nullable: false),
                    BlockRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    BlockGraduation = table.Column<bool>(type: "boolean", nullable: false),
                    BlockDocuments = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_policies", x => x.Id);
                    table.UniqueConstraint("AK_finance_policies_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_finance_policies_Id_InstitutionId_Currency", x => new { x.Id, x.InstitutionId, x.Currency });
                    table.CheckConstraint("CK_finance_policies_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_policy", "\"Status\" IN ('Published','Retired') AND \"EffectiveFrom\" < \"EffectiveUntil\" AND \"Revision\" > 0 AND \"MaximumInstallments\" BETWEEN 1 AND 60 AND \"MaximumExtensionDays\" BETWEEN 1 AND 730 AND \"GraceDays\" BETWEEN 0 AND 365 AND \"OverdueThresholdMinor\" BETWEEN 0 AND 9000000000000000");
                    table.ForeignKey(
                        name: "FK_finance_policies_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProvidedRowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_reconciliations", x => x.Id);
                    table.UniqueConstraint("AK_finance_reconciliations_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_finance_reconciliations_identity_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_reconciliations_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_scholarships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    BasisPoints = table.Column<int>(type: "integer", nullable: false),
                    FixedMinor = table.Column<long>(type: "bigint", nullable: false),
                    FundingLimitMinor = table.Column<long>(type: "bigint", nullable: true),
                    MinimumGpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    RequireGoodStanding = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_scholarships", x => x.Id);
                    table.UniqueConstraint("AK_finance_scholarships_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_scholarships_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_scholarship", "\"Revision\" > 0 AND \"EffectiveFrom\" < \"EffectiveUntil\" AND \"Mode\" IN ('Percent','Fixed','Full') AND \"BasisPoints\" BETWEEN 0 AND 10000 AND \"FixedMinor\" BETWEEN 0 AND 9000000000000000 AND (\"Mode\" <> 'Percent' OR \"BasisPoints\" > 0) AND (\"Mode\" <> 'Fixed' OR \"FixedMinor\" > 0) AND (\"FundingLimitMinor\" IS NULL OR \"FundingLimitMinor\" BETWEEN 0 AND 9000000000000000) AND (\"MinimumGpa\" IS NULL OR \"MinimumGpa\" BETWEEN 0 AND 4)");
                    table.ForeignKey(
                        name: "FK_finance_scholarships_academic_programs_ProgramId_Institutio~",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_scholarships_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_scholarships_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_audit_events", x => x.Id);
                    table.UniqueConstraint("AK_finance_audit_events_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_finance_audit_events_finance_accounts_AccountId_Institution~",
                        columns: x => new { x.AccountId, x.InstitutionId },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_audit_events_identity_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_audit_events_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Method = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    RequiresVerification = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegisteredBy = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_payments", x => x.Id);
                    table.UniqueConstraint("AK_finance_payments_Id_AccountId_InstitutionId_Currency", x => new { x.Id, x.AccountId, x.InstitutionId, x.Currency });
                    table.UniqueConstraint("AK_finance_payments_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_payments_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_payment", "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Status\" IN ('Pending','Verified','Posted','Rejected','Reversed') AND \"Method\" IN ('Cash','BankTransfer','ManualTransfer','ExternalReference') AND (\"Status\" NOT IN ('Verified','Posted','Reversed') OR NOT \"RequiresVerification\" OR (\"VerifiedBy\" IS NOT NULL AND \"VerifiedBy\" <> \"RegisteredBy\"))");
                    table.ForeignKey(
                        name: "FK_finance_payments_finance_accounts_AccountId_InstitutionId_C~",
                        columns: x => new { x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payments_identity_users_RegisteredBy",
                        column: x => x.RegisteredBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payments_identity_users_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_fee_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    UnitMinor = table.Column<long>(type: "bigint", nullable: false),
                    Optional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_fee_items", x => x.Id);
                    table.UniqueConstraint("AK_finance_fee_items_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_fee", "\"UnitMinor\" BETWEEN 1 AND 9000000000000000 AND \"Mode\" IN ('Fixed','PerCredit') AND \"Category\" IN ('Tuition','Registration','Laboratory','Service','Optional')");
                    table.ForeignKey(
                        name: "FK_finance_fee_items_courses_CourseId_InstitutionId",
                        columns: x => new { x.CourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_fee_items_finance_fee_schedules_ScheduleId_Institut~",
                        columns: x => new { x.ScheduleId, x.InstitutionId },
                        principalTable: "finance_fee_schedules",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_fee_items_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PricingEvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    OptionalCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_invoices", x => x.Id);
                    table.UniqueConstraint("AK_finance_invoices_Id_AccountId_InstitutionId_Currency", x => new { x.Id, x.AccountId, x.InstitutionId, x.Currency });
                    table.UniqueConstraint("AK_finance_invoices_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_invoices_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_invoice", "\"Status\" IN ('Draft','Posted','Reversed') AND \"Scope\"='Tuition' AND \"TotalMinor\" BETWEEN 1 AND 9000000000000000 AND (\"Status\"='Draft' OR \"PostedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_finance_invoices_academic_programs_ProgramId_InstitutionId",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoices_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoices_finance_accounts_AccountId_InstitutionId_C~",
                        columns: x => new { x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoices_finance_fee_schedules_ScheduleId_Instituti~",
                        columns: x => new { x.ScheduleId, x.InstitutionId, x.Currency },
                        principalTable: "finance_fee_schedules",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoices_finance_policies_PolicyId_InstitutionId_Cu~",
                        columns: x => new { x.PolicyId, x.InstitutionId, x.Currency },
                        principalTable: "finance_policies",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoices_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Proof = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_receipts", x => x.Id);
                    table.UniqueConstraint("AK_finance_receipts_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_finance_receipts_finance_payments_PaymentId_InstitutionId",
                        columns: x => new { x.PaymentId, x.InstitutionId },
                        principalTable: "finance_payments",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_receipts_institutional_verification_keys_Verificati~",
                        columns: x => new { x.VerificationKeyId, x.InstitutionId },
                        principalTable: "institutional_verification_keys",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_receipts_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExecutionReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_refunds", x => x.Id);
                    table.UniqueConstraint("AK_finance_refunds_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_refunds_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_refund", "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Status\" IN ('Requested','Approved','Rejected','Executed','Reversed') AND (\"Status\" NOT IN ('Approved','Executed','Reversed') OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\")) AND (\"Status\" NOT IN ('Executed','Reversed') OR \"ExecutionReference\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_finance_refunds_finance_accounts_AccountId_InstitutionId_Cu~",
                        columns: x => new { x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_refunds_finance_payments_PaymentId_AccountId_Instit~",
                        columns: x => new { x.PaymentId, x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_payments",
                        principalColumns: new[] { "Id", "AccountId", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_refunds_finance_policies_PolicyId_InstitutionId_Cur~",
                        columns: x => new { x.PolicyId, x.InstitutionId, x.Currency },
                        principalTable: "finance_policies",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_refunds_identity_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_refunds_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_refunds_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_awards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScholarshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ApprovalEvidenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_awards", x => x.Id);
                    table.UniqueConstraint("AK_finance_awards_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_award", "\"Status\" IN ('Requested','Approved','Rejected','Revoked') AND (\"Status\" NOT IN ('Approved','Revoked') OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\" AND \"ApprovalEvidenceJson\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_finance_awards_finance_invoices_InvoiceId_InstitutionId",
                        columns: x => new { x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_awards_finance_scholarships_ScholarshipId_Instituti~",
                        columns: x => new { x.ScholarshipId, x.InstitutionId },
                        principalTable: "finance_scholarships",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_awards_identity_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_awards_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_awards_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UnitMinor = table.Column<long>(type: "bigint", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_invoice_lines", x => x.Id);
                    table.UniqueConstraint("AK_finance_invoice_lines_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_finance_invoice_lines_Id_InvoiceId_InstitutionId", x => new { x.Id, x.InvoiceId, x.InstitutionId });
                    table.CheckConstraint("CK_p21_invoice_line", "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"UnitMinor\" BETWEEN 1 AND 9000000000000000 AND \"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_finance_invoice_lines_finance_fee_items_FeeItemId_Instituti~",
                        columns: x => new { x.FeeItemId, x.InstitutionId },
                        principalTable: "finance_fee_items",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoice_lines_finance_invoices_InvoiceId_Institutio~",
                        columns: x => new { x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_invoice_lines_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Kind = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesJournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_journals", x => x.Id);
                    table.UniqueConstraint("AK_finance_journals_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_journals_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_journal_amount", "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Kind\" IN ('InvoicePosted','DebitAdjustment','CreditNote','PaymentPosted','PaymentReversed','Allocation','AllocationReversal','ScholarshipGranted','ScholarshipReclaimed','RefundExecuted','RefundReversed')");
                    table.ForeignKey(
                        name: "FK_finance_journals_finance_accounts_AccountId_InstitutionId_C~",
                        columns: x => new { x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journals_finance_invoices_InvoiceId_AccountId_Insti~",
                        columns: x => new { x.InvoiceId, x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "AccountId", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journals_finance_journals_ReversesJournalId_Institu~",
                        columns: x => new { x.ReversesJournalId, x.InstitutionId },
                        principalTable: "finance_journals",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journals_finance_payments_PaymentId_AccountId_Insti~",
                        columns: x => new { x.PaymentId, x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_payments",
                        principalColumns: new[] { "Id", "AccountId", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journals_identity_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journals_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_payment_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseDebtMinor = table.Column<long>(type: "bigint", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_payment_plans", x => x.Id);
                    table.UniqueConstraint("AK_finance_payment_plans_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_plan", "\"Status\" IN ('Proposed','Approved','Rejected','Superseded') AND \"Revision\" > 0 AND \"BaseDebtMinor\" BETWEEN 1 AND 9000000000000000 AND (\"Status\" NOT IN ('Approved','Superseded') OR \"ApprovedAtUtc\" IS NOT NULL) AND (\"Status\" <> 'Superseded' OR \"SupersededAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_finance_payment_plans_finance_invoices_InvoiceId_Institutio~",
                        columns: x => new { x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payment_plans_identity_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payment_plans_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_payment_plans_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_adjustments", x => x.Id);
                    table.UniqueConstraint("AK_finance_adjustments_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_adjustment", "\"SignedMinor\" <> 0 AND \"SignedMinor\" BETWEEN -9000000000000000 AND 9000000000000000");
                    table.ForeignKey(
                        name: "FK_finance_adjustments_finance_invoice_lines_InvoiceLineId_Inv~",
                        columns: x => new { x.InvoiceLineId, x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoice_lines",
                        principalColumns: new[] { "Id", "InvoiceId", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_adjustments_finance_invoices_InvoiceId_InstitutionId",
                        columns: x => new { x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_adjustments_finance_journals_JournalId_InstitutionId",
                        columns: x => new { x.JournalId, x.InstitutionId },
                        principalTable: "finance_journals",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_adjustments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalAllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignedMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_allocations", x => x.Id);
                    table.UniqueConstraint("AK_finance_allocations_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_finance_allocations_currency", "\"Currency\" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')");
                    table.CheckConstraint("CK_p21_allocation", "(\"SignedMinor\" > 0 AND \"OriginalAllocationId\" IS NULL) OR (\"SignedMinor\" < 0 AND \"OriginalAllocationId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_finance_allocations_finance_accounts_AccountId_InstitutionI~",
                        columns: x => new { x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_allocations_finance_allocations_OriginalAllocationI~",
                        columns: x => new { x.OriginalAllocationId, x.InstitutionId },
                        principalTable: "finance_allocations",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_allocations_finance_invoices_InvoiceId_AccountId_In~",
                        columns: x => new { x.InvoiceId, x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "AccountId", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_allocations_finance_journals_JournalId_InstitutionId",
                        columns: x => new { x.JournalId, x.InstitutionId },
                        principalTable: "finance_journals",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_allocations_finance_payments_PaymentId_AccountId_In~",
                        columns: x => new { x.PaymentId, x.AccountId, x.InstitutionId, x.Currency },
                        principalTable: "finance_payments",
                        principalColumns: new[] { "Id", "AccountId", "InstitutionId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_allocations_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_journal_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignedMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_journal_lines", x => x.Id);
                    table.UniqueConstraint("AK_finance_journal_lines_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_leg", "\"SignedMinor\" <> 0 AND \"SignedMinor\" BETWEEN -9000000000000000 AND 9000000000000000 AND \"Bucket\" IN ('Receivable','StudentCredit','Funds','Revenue','Aid')");
                    table.ForeignKey(
                        name: "FK_finance_journal_lines_finance_journals_JournalId_Institutio~",
                        columns: x => new { x.JournalId, x.InstitutionId },
                        principalTable: "finance_journals",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_journal_lines_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_holds", x => x.Id);
                    table.UniqueConstraint("AK_finance_holds_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_hold", "\"Status\" IN ('Open','Released') AND \"Scope\" IN ('Registration','Graduation','Documents') AND (\"Status\" <> 'Released' OR \"ReleasedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_finance_holds_finance_accounts_AccountId_InstitutionId",
                        columns: x => new { x.AccountId, x.InstitutionId },
                        principalTable: "finance_accounts",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_holds_finance_invoices_InvoiceId_InstitutionId",
                        columns: x => new { x.InvoiceId, x.InstitutionId },
                        principalTable: "finance_invoices",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_holds_finance_payment_plans_PlanId_InstitutionId",
                        columns: x => new { x.PlanId, x.InstitutionId },
                        principalTable: "finance_payment_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_holds_finance_policies_PolicyId_InstitutionId",
                        columns: x => new { x.PolicyId, x.InstitutionId },
                        principalTable: "finance_policies",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_holds_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_installments", x => x.Id);
                    table.UniqueConstraint("AK_finance_installments_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p21_installment", "\"Position\" > 0 AND \"AmountMinor\" BETWEEN 1 AND 9000000000000000");
                    table.ForeignKey(
                        name: "FK_finance_installments_finance_payment_plans_PlanId_Instituti~",
                        columns: x => new { x.PlanId, x.InstitutionId },
                        principalTable: "finance_payment_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_installments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_accounts_InstitutionId_StudentId_Currency",
                table: "finance_accounts",
                columns: new[] { "InstitutionId", "StudentId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_accounts_StudentId",
                table: "finance_accounts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_adjustments_InstitutionId",
                table: "finance_adjustments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_adjustments_InvoiceId_InstitutionId",
                table: "finance_adjustments",
                columns: new[] { "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_adjustments_InvoiceLineId_InvoiceId_InstitutionId",
                table: "finance_adjustments",
                columns: new[] { "InvoiceLineId", "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_adjustments_JournalId",
                table: "finance_adjustments",
                column: "JournalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_adjustments_JournalId_InstitutionId",
                table: "finance_adjustments",
                columns: new[] { "JournalId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_AccountId_InstitutionId_Currency",
                table: "finance_allocations",
                columns: new[] { "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_InstitutionId",
                table: "finance_allocations",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_InvoiceId_AccountId_InstitutionId_Curre~",
                table: "finance_allocations",
                columns: new[] { "InvoiceId", "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_JournalId",
                table: "finance_allocations",
                column: "JournalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_JournalId_InstitutionId",
                table: "finance_allocations",
                columns: new[] { "JournalId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_OriginalAllocationId_InstitutionId",
                table: "finance_allocations",
                columns: new[] { "OriginalAllocationId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_allocations_PaymentId_AccountId_InstitutionId_Curre~",
                table: "finance_allocations",
                columns: new[] { "PaymentId", "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_audit_events_AccountId_InstitutionId",
                table: "finance_audit_events",
                columns: new[] { "AccountId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_audit_events_ActorId",
                table: "finance_audit_events",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_audit_events_InstitutionId_AccountId_AtUtc",
                table: "finance_audit_events",
                columns: new[] { "InstitutionId", "AccountId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_authorities_InstitutionId_UserId",
                table: "finance_authorities",
                columns: new[] { "InstitutionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_authorities_UserId",
                table: "finance_authorities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_InstitutionId",
                table: "finance_awards",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_InvoiceId_InstitutionId",
                table: "finance_awards",
                columns: new[] { "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_InvoiceId_ScholarshipId",
                table: "finance_awards",
                columns: new[] { "InvoiceId", "ScholarshipId" },
                unique: true,
                filter: "\"Status\" IN ('Requested','Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_RequestedBy",
                table: "finance_awards",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_ReviewedBy",
                table: "finance_awards",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_ScholarshipId_InstitutionId",
                table: "finance_awards",
                columns: new[] { "ScholarshipId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_commands_InstitutionId_RequestId",
                table: "finance_commands",
                columns: new[] { "InstitutionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_items_CourseId_InstitutionId",
                table: "finance_fee_items",
                columns: new[] { "CourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_items_InstitutionId",
                table: "finance_fee_items",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_items_ScheduleId_Code",
                table: "finance_fee_items",
                columns: new[] { "ScheduleId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_items_ScheduleId_InstitutionId",
                table: "finance_fee_items",
                columns: new[] { "ScheduleId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_schedules_InstitutionId_Code_Currency_Revision",
                table: "finance_fee_schedules",
                columns: new[] { "InstitutionId", "Code", "Currency", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_schedules_ProgramId_InstitutionId",
                table: "finance_fee_schedules",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_fee_schedules_TermId_InstitutionId",
                table: "finance_fee_schedules",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_AccountId_InstitutionId",
                table: "finance_holds",
                columns: new[] { "AccountId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_AccountId_Status_EffectiveFrom",
                table: "finance_holds",
                columns: new[] { "AccountId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_InstitutionId",
                table: "finance_holds",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_InvoiceId_InstitutionId",
                table: "finance_holds",
                columns: new[] { "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_InvoiceId_Scope",
                table: "finance_holds",
                columns: new[] { "InvoiceId", "Scope" },
                unique: true,
                filter: "\"Status\"='Open'");

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_PlanId_InstitutionId",
                table: "finance_holds",
                columns: new[] { "PlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_holds_PolicyId_InstitutionId",
                table: "finance_holds",
                columns: new[] { "PolicyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_installments_InstitutionId",
                table: "finance_installments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_installments_PlanId_InstitutionId",
                table: "finance_installments",
                columns: new[] { "PlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_installments_PlanId_Position",
                table: "finance_installments",
                columns: new[] { "PlanId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_lines_FeeItemId_InstitutionId",
                table: "finance_invoice_lines",
                columns: new[] { "FeeItemId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_lines_InstitutionId",
                table: "finance_invoice_lines",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_lines_InvoiceId_InstitutionId",
                table: "finance_invoice_lines",
                columns: new[] { "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_AccountId_InstitutionId_Currency",
                table: "finance_invoices",
                columns: new[] { "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_AccountId_TermId_Scope",
                table: "finance_invoices",
                columns: new[] { "AccountId", "TermId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_InstitutionId_Currency_DueDate",
                table: "finance_invoices",
                columns: new[] { "InstitutionId", "Currency", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_PolicyId_InstitutionId_Currency",
                table: "finance_invoices",
                columns: new[] { "PolicyId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_ProgramId_InstitutionId",
                table: "finance_invoices",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_ScheduleId_InstitutionId_Currency",
                table: "finance_invoices",
                columns: new[] { "ScheduleId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoices_TermId_InstitutionId",
                table: "finance_invoices",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journal_lines_InstitutionId",
                table: "finance_journal_lines",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_journal_lines_JournalId_Bucket",
                table: "finance_journal_lines",
                columns: new[] { "JournalId", "Bucket" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_journal_lines_JournalId_InstitutionId",
                table: "finance_journal_lines",
                columns: new[] { "JournalId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_AccountId_InstitutionId_Currency",
                table: "finance_journals",
                columns: new[] { "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_AccountId_PostedAtUtc",
                table: "finance_journals",
                columns: new[] { "AccountId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_ActorId",
                table: "finance_journals",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_InstitutionId_PostingKey",
                table: "finance_journals",
                columns: new[] { "InstitutionId", "PostingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_InvoiceId_AccountId_InstitutionId_Currency",
                table: "finance_journals",
                columns: new[] { "InvoiceId", "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_Kind_SourceId",
                table: "finance_journals",
                columns: new[] { "Kind", "SourceId" },
                unique: true,
                filter: "\"Kind\" IN ('InvoicePosted','PaymentPosted','PaymentReversed','RefundExecuted','RefundReversed')");

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_PaymentId_AccountId_InstitutionId_Currency",
                table: "finance_journals",
                columns: new[] { "PaymentId", "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_ReversesJournalId",
                table: "finance_journals",
                column: "ReversesJournalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_ReversesJournalId_InstitutionId",
                table: "finance_journals",
                columns: new[] { "ReversesJournalId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_plans_InstitutionId",
                table: "finance_payment_plans",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_plans_InvoiceId_InstitutionId",
                table: "finance_payment_plans",
                columns: new[] { "InvoiceId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_plans_InvoiceId_Revision",
                table: "finance_payment_plans",
                columns: new[] { "InvoiceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_plans_RequestedBy",
                table: "finance_payment_plans",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_plans_ReviewedBy",
                table: "finance_payment_plans",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_plan_approved",
                table: "finance_payment_plans",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\"='Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_finance_plan_proposed",
                table: "finance_payment_plans",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\"='Proposed'");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payments_AccountId_InstitutionId_Currency",
                table: "finance_payments",
                columns: new[] { "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payments_InstitutionId_Reference",
                table: "finance_payments",
                columns: new[] { "InstitutionId", "Reference" },
                unique: true,
                filter: "\"Status\" <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payments_RegisteredBy",
                table: "finance_payments",
                column: "RegisteredBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_payments_VerifiedBy",
                table: "finance_payments",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_policies_InstitutionId_Currency_Revision",
                table: "finance_policies",
                columns: new[] { "InstitutionId", "Currency", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_receipts_InstitutionId",
                table: "finance_receipts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_receipts_Number",
                table: "finance_receipts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_receipts_PaymentId",
                table: "finance_receipts",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_receipts_PaymentId_InstitutionId",
                table: "finance_receipts",
                columns: new[] { "PaymentId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_receipts_VerificationKeyId_InstitutionId",
                table: "finance_receipts",
                columns: new[] { "VerificationKeyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_reconciliations_ActorId",
                table: "finance_reconciliations",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_reconciliations_InstitutionId",
                table: "finance_reconciliations",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_AccountId_InstitutionId_Currency",
                table: "finance_refunds",
                columns: new[] { "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_InstitutionId_ExecutionReference",
                table: "finance_refunds",
                columns: new[] { "InstitutionId", "ExecutionReference" },
                unique: true,
                filter: "\"ExecutionReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_PaymentId_AccountId_InstitutionId_Currency",
                table: "finance_refunds",
                columns: new[] { "PaymentId", "AccountId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_PolicyId_InstitutionId_Currency",
                table: "finance_refunds",
                columns: new[] { "PolicyId", "InstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_RequestedBy",
                table: "finance_refunds",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_refunds_ReviewedBy",
                table: "finance_refunds",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_finance_scholarships_InstitutionId_Code_Currency_Revision",
                table: "finance_scholarships",
                columns: new[] { "InstitutionId", "Code", "Currency", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_scholarships_ProgramId_InstitutionId",
                table: "finance_scholarships",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_scholarships_TermId_InstitutionId",
                table: "finance_scholarships",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.DropCheckConstraint(
                name: "CK_p21_payment",
                table: "finance_payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_p21_invoice_line",
                table: "finance_invoice_lines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p21_payment",
                table: "finance_payments",
                sql: "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Reference\"=upper(btrim(\"Reference\")) AND \"Status\" IN ('Pending','Verified','Posted','Rejected','Reversed') AND \"Method\" IN ('Cash','BankTransfer','ManualTransfer','ExternalReference') AND (\"Status\" NOT IN ('Posted','Reversed') OR \"PostedAtUtc\" IS NOT NULL) AND (\"Status\" NOT IN ('Verified','Posted','Reversed') OR NOT \"RequiresVerification\" OR (\"VerifiedBy\" IS NOT NULL AND \"VerifiedBy\" <> \"RegisteredBy\"))");

            migrationBuilder.CreateIndex(
                name: "IX_finance_journals_SourceId",
                table: "finance_journals",
                column: "SourceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p21_invoice_line",
                table: "finance_invoice_lines",
                sql: "\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"UnitMinor\" BETWEEN 1 AND 9000000000000000 AND \"Quantity\" > 0 AND \"Quantity\" <= 10000 AND \"AmountMinor\"=round(\"UnitMinor\"::numeric*\"Quantity\",0)");

            migrationBuilder.DropIndex(
                name: "IX_finance_awards_ScholarshipId_InstitutionId",
                table: "finance_awards");

            migrationBuilder.AddColumn<string>(
                name: "ProgramCode",
                table: "finance_awards",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "finance_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_finance_scholarships_Id_InstitutionId_Code",
                table: "finance_scholarships",
                columns: new[] { "Id", "InstitutionId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_InvoiceId_ProgramCode",
                table: "finance_awards",
                columns: new[] { "InvoiceId", "ProgramCode" },
                unique: true,
                filter: "\"Status\" IN ('Requested','Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_finance_awards_ScholarshipId_InstitutionId_ProgramCode",
                table: "finance_awards",
                columns: new[] { "ScholarshipId", "InstitutionId", "ProgramCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_finance_awards_finance_scholarships_ScholarshipId_Institut~1",
                table: "finance_awards",
                columns: new[] { "ScholarshipId", "InstitutionId", "ProgramCode" },
                principalTable: "finance_scholarships",
                principalColumns: new[] { "Id", "InstitutionId", "Code" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(IntegritySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropIntegritySql);
            migrationBuilder.DropTable(
                name: "finance_adjustments");

            migrationBuilder.DropTable(
                name: "finance_allocations");

            migrationBuilder.DropTable(
                name: "finance_audit_events");

            migrationBuilder.DropTable(
                name: "finance_authorities");

            migrationBuilder.DropTable(
                name: "finance_awards");

            migrationBuilder.DropTable(
                name: "finance_commands");

            migrationBuilder.DropTable(
                name: "finance_holds");

            migrationBuilder.DropTable(
                name: "finance_installments");

            migrationBuilder.DropTable(
                name: "finance_journal_lines");

            migrationBuilder.DropTable(
                name: "finance_receipts");

            migrationBuilder.DropTable(
                name: "finance_reconciliations");

            migrationBuilder.DropTable(
                name: "finance_refunds");

            migrationBuilder.DropTable(
                name: "finance_invoice_lines");

            migrationBuilder.DropTable(
                name: "finance_scholarships");

            migrationBuilder.DropTable(
                name: "finance_payment_plans");

            migrationBuilder.DropTable(
                name: "finance_journals");

            migrationBuilder.DropTable(
                name: "finance_fee_items");

            migrationBuilder.DropTable(
                name: "finance_invoices");

            migrationBuilder.DropTable(
                name: "finance_payments");

            migrationBuilder.DropTable(
                name: "finance_fee_schedules");

            migrationBuilder.DropTable(
                name: "finance_policies");

            migrationBuilder.DropTable(
                name: "finance_accounts");
        }
    }
}





