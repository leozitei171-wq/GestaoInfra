using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaGestaoInfra.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class CicloDeVidaAtividade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_atividades_StatusId",
                table: "atividades");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DataInicioPrevisto",
                table: "atividades",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DataFimPrevisto",
                table: "atividades",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicioReal",
                table: "atividades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoNegativa",
                table: "atividades",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_atividades_StatusId_DataFimPrevisto",
                table: "atividades",
                columns: new[] { "StatusId", "DataFimPrevisto" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_atividades_StatusId_DataFimPrevisto",
                table: "atividades");

            migrationBuilder.DropColumn(
                name: "DataInicioReal",
                table: "atividades");

            migrationBuilder.DropColumn(
                name: "MotivoNegativa",
                table: "atividades");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DataInicioPrevisto",
                table: "atividades",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DataFimPrevisto",
                table: "atividades",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_atividades_StatusId",
                table: "atividades",
                column: "StatusId");
        }
    }
}
