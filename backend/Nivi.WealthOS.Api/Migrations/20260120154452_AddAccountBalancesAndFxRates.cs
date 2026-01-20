using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nivi.WealthOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalancesAndFxRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BilledAmount",
                table: "Accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastStatementDate",
                table: "Accounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalanceBaseAmount",
                table: "Accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalanceFxRateUsed",
                table: "Accounts",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnbilledAmount",
                table: "Accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BilledAmount",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LastStatementDate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceBaseAmount",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceFxRateUsed",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UnbilledAmount",
                table: "Accounts");
        }
    }
}
