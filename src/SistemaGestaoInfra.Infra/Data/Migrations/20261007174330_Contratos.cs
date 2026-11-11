using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaGestaoInfra.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class Contratos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RazaoSocial = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Contato = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_medicao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_medicao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_servico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_servico", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contratos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FornecedorId = table.Column<int>(type: "integer", nullable: false),
                    SetorId = table.Column<int>(type: "integer", nullable: true),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ArquivoPdfPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contratos_fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contratos_setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contrato_alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    Nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PercentualUso = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Lido = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contrato_alertas_contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contrato_servicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    TipoServicoId = table.Column<int>(type: "integer", nullable: false),
                    TipoMedicaoId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Limite = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_servicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contrato_servicos_contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contrato_servicos_tipos_medicao_TipoMedicaoId",
                        column: x => x.TipoMedicaoId,
                        principalTable: "tipos_medicao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contrato_servicos_tipos_servico_TipoServicoId",
                        column: x => x.TipoServicoId,
                        principalTable: "tipos_servico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "atividade_servicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AtividadeId = table.Column<int>(type: "integer", nullable: false),
                    ContratoServicoId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrefeituraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atividade_servicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_atividade_servicos_atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_atividade_servicos_contrato_servicos_ContratoServicoId",
                        column: x => x.ContratoServicoId,
                        principalTable: "contrato_servicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_atividade_servicos_AtividadeId",
                table: "atividade_servicos",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_atividade_servicos_ContratoServicoId",
                table: "atividade_servicos",
                column: "ContratoServicoId");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_alertas_ContratoId",
                table: "contrato_alertas",
                column: "ContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_alertas_PrefeituraId_Lido",
                table: "contrato_alertas",
                columns: new[] { "PrefeituraId", "Lido" });

            migrationBuilder.CreateIndex(
                name: "IX_contrato_servicos_ContratoId_TipoServicoId",
                table: "contrato_servicos",
                columns: new[] { "ContratoId", "TipoServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrato_servicos_TipoMedicaoId",
                table: "contrato_servicos",
                column: "TipoMedicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_servicos_TipoServicoId",
                table: "contrato_servicos",
                column: "TipoServicoId");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_DataFim",
                table: "contratos",
                column: "DataFim");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_FornecedorId",
                table: "contratos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_PrefeituraId_Codigo",
                table: "contratos",
                columns: new[] { "PrefeituraId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contratos_SetorId",
                table: "contratos",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_PrefeituraId_Cnpj",
                table: "fornecedores",
                columns: new[] { "PrefeituraId", "Cnpj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_medicao_PrefeituraId_Codigo",
                table: "tipos_medicao",
                columns: new[] { "PrefeituraId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_servico_PrefeituraId_Codigo",
                table: "tipos_servico",
                columns: new[] { "PrefeituraId", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atividade_servicos");

            migrationBuilder.DropTable(
                name: "contrato_alertas");

            migrationBuilder.DropTable(
                name: "contrato_servicos");

            migrationBuilder.DropTable(
                name: "contratos");

            migrationBuilder.DropTable(
                name: "tipos_medicao");

            migrationBuilder.DropTable(
                name: "tipos_servico");

            migrationBuilder.DropTable(
                name: "fornecedores");
        }
    }
}
