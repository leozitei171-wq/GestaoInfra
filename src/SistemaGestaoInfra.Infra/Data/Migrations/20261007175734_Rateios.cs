using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaGestaoInfra.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rateios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_custo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_custo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rateios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataRateio = table.Column<DateOnly>(type: "date", nullable: false),
                    Modo = table.Column<int>(type: "integer", nullable: false),
                    TipoCustoId = table.Column<int>(type: "integer", nullable: true),
                    CriadoPorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rateios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rateios_tipos_custo_TipoCustoId",
                        column: x => x.TipoCustoId,
                        principalTable: "tipos_custo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rateio_itens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RateioId = table.Column<int>(type: "integer", nullable: false),
                    ContratoId = table.Column<int>(type: "integer", nullable: true),
                    AtividadeId = table.Column<int>(type: "integer", nullable: true),
                    Base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rateio_itens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rateio_itens_atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rateio_itens_contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rateio_itens_rateios_RateioId",
                        column: x => x.RateioId,
                        principalTable: "rateios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rateio_itens_AtividadeId",
                table: "rateio_itens",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_rateio_itens_ContratoId",
                table: "rateio_itens",
                column: "ContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_rateio_itens_RateioId",
                table: "rateio_itens",
                column: "RateioId");

            migrationBuilder.CreateIndex(
                name: "IX_rateios_PrefeituraId_DataRateio",
                table: "rateios",
                columns: new[] { "PrefeituraId", "DataRateio" });

            migrationBuilder.CreateIndex(
                name: "IX_rateios_TipoCustoId",
                table: "rateios",
                column: "TipoCustoId");

            migrationBuilder.CreateIndex(
                name: "IX_tipos_custo_PrefeituraId_Codigo",
                table: "tipos_custo",
                columns: new[] { "PrefeituraId", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rateio_itens");

            migrationBuilder.DropTable(
                name: "rateios");

            migrationBuilder.DropTable(
                name: "tipos_custo");
        }
    }
}
