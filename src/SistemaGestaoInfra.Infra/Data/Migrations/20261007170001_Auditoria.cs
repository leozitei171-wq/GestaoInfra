using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaGestaoInfra.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuandoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Acao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntidadeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_Entidade",
                table: "auditoria",
                column: "Entidade");

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_PrefeituraId_QuandoEm",
                table: "auditoria",
                columns: new[] { "PrefeituraId", "QuandoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria");
        }
    }
}
