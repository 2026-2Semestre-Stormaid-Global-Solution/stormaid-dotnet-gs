using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stormaid.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Baseline do schema Oracle existente do Storm Aid.
    /// As tabelas e os índices iniciais são provisionados pelos scripts SQL do projeto.
    /// </summary>
    public partial class BaselineExistingOracleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O schema inicial já existe no Oracle.
            // Nenhuma estrutura deve ser recriada nesta baseline.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A baseline não é proprietária das tabelas existentes.
            // O rollback não deve excluir estruturas nem dados.
        }
    }
}