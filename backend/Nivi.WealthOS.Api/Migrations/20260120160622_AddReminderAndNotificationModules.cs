using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nivi.WealthOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderAndNotificationModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalTargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FxRateUsed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    BaseTargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InsurancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OriginalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalPremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FxRateUsed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    BasePremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RenewalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsurancePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BorrowerName = table.Column<string>(type: "text", nullable: true),
                    OriginalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FxRateUsed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmiAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AvailableAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SendEmail = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastNotifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FxRateUsed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeadlines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeadlines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSmtpSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSmtpSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_TenantId_CheckInDate",
                table: "Goals",
                columns: new[] { "TenantId", "CheckInDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_TenantId_EntityId",
                table: "Goals",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_InsurancePolicies_TenantId_EntityId",
                table: "InsurancePolicies",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_InsurancePolicies_TenantId_RenewalDate",
                table: "InsurancePolicies",
                columns: new[] { "TenantId", "RenewalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_TenantId_EntityId",
                table: "Loans",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_TenantId_NextDueDate",
                table: "Loans",
                columns: new[] { "TenantId", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_UserId_AvailableAtUtc",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TenantId_DueDate",
                table: "Reminders",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TenantId_UserId",
                table: "Reminders",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_EntityId",
                table: "Subscriptions",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_NextDueDate",
                table: "Subscriptions",
                columns: new[] { "TenantId", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeadlines_TenantId_DueDate",
                table: "TaxDeadlines",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeadlines_TenantId_EntityId",
                table: "TaxDeadlines",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSmtpSettings_TenantId",
                table: "TenantSmtpSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "InsurancePolicies");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TaxDeadlines");

            migrationBuilder.DropTable(
                name: "TenantSmtpSettings");
        }
    }
}
