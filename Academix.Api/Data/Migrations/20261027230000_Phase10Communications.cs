using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase10Communications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "communication_threads",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberLowId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberHighId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastReadLowAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastReadHighAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_communication_threads", x => x.Id);
                table.UniqueConstraint("AK_communication_threads_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.ForeignKey(name: "FK_p10_thread_section", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "course_sections", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p10_thread_low", column: x => x.MemberLowId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p10_thread_high", column: x => x.MemberHighId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_communication_threads_SectionId_MemberLowId_MemberHighId", table: "communication_threads", columns: new[] { "SectionId", "MemberLowId", "MemberHighId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_communication_threads_SectionId_InstitutionId", table: "communication_threads", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_communication_threads_MemberLowId", table: "communication_threads", column: "MemberLowId");
        migrationBuilder.CreateIndex(name: "IX_communication_threads_MemberHighId", table: "communication_threads", column: "MemberHighId");

        migrationBuilder.CreateTable(
            name: "communication_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_communication_messages", x => x.Id);
                table.ForeignKey(name: "FK_p10_message_thread", columns: x => new { x.ThreadId, x.InstitutionId }, principalTable: "communication_threads", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p10_message_sender", column: x => x.SenderId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_communication_messages_ThreadId_SenderId_ClientMessageId", table: "communication_messages", columns: new[] { "ThreadId", "SenderId", "ClientMessageId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_communication_messages_ThreadId_CreatedAtUtc", table: "communication_messages", columns: new[] { "ThreadId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_communication_messages_ThreadId_InstitutionId", table: "communication_messages", columns: new[] { "ThreadId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_communication_messages_SenderId", table: "communication_messages", column: "SenderId");

        migrationBuilder.CreateTable(
            name: "communication_notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_communication_notifications", x => x.Id);
                table.ForeignKey(name: "FK_p10_notification_recipient", column: x => x.RecipientId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_communication_notifications_RecipientId_Kind_SourceId", table: "communication_notifications", columns: new[] { "RecipientId", "Kind", "SourceId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_communication_notifications_RecipientId_CreatedAtUtc", table: "communication_notifications", columns: new[] { "RecipientId", "CreatedAtUtc" });

        migrationBuilder.CreateTable(
            name: "communication_preferences",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                MessageNotifications = table.Column<bool>(type: "boolean", nullable: false),
                AnnouncementNotifications = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_communication_preferences", x => x.UserId);
                table.ForeignKey(name: "FK_p10_preference_user", column: x => x.UserId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });


        migrationBuilder.CreateTable(
            name: "communication_announcements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Audience = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ScheduledForUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_communication_announcements", x => x.Id);
                table.ForeignKey(name: "FK_p10_announcement_section", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "course_sections", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p10_announcement_author", column: x => x.AuthorId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_communication_announcements_SectionId_ScheduledForUtc", table: "communication_announcements", columns: new[] { "SectionId", "ScheduledForUtc" });
        migrationBuilder.CreateIndex(name: "IX_communication_announcements_SectionId_InstitutionId", table: "communication_announcements", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_communication_announcements_AuthorId", table: "communication_announcements", column: "AuthorId");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "communication_announcements");
        migrationBuilder.DropTable(name: "communication_preferences");
        migrationBuilder.DropTable(name: "communication_notifications");
        migrationBuilder.DropTable(name: "communication_messages");
        migrationBuilder.DropTable(name: "communication_threads");
    }
}
