using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchTokensAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppMetas",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMetas", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "MedicineSearchTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MedicineId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    TokenType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineSearchTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineSearchTokens_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientSearchTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    TokenType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientSearchTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientSearchTokens_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineSearchTokens_MedicineId",
                table: "MedicineSearchTokens",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineSearchTokens_Token_MedicineId",
                table: "MedicineSearchTokens",
                columns: new[] { "Token", "MedicineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientSearchTokens_PatientId",
                table: "PatientSearchTokens",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSearchTokens_Token_PatientId",
                table: "PatientSearchTokens",
                columns: new[] { "Token", "PatientId" });

            // SQL Backfill: Initial tokens via recursive CTE (Amendment 2)
            migrationBuilder.Sql(@"
WITH RECURSIVE split_patient_names(PatientId, word, str) AS (
    SELECT Id,
           CASE WHEN INSTR(TRIM(NormalizedName), ' ') > 0
                THEN SUBSTR(TRIM(NormalizedName), 1, INSTR(TRIM(NormalizedName), ' ') - 1)
                ELSE TRIM(NormalizedName) END,
           CASE WHEN INSTR(TRIM(NormalizedName), ' ') > 0
                THEN SUBSTR(TRIM(NormalizedName), INSTR(TRIM(NormalizedName), ' ') + 1)
                ELSE '' END
    FROM Patients
    WHERE NormalizedName IS NOT NULL AND TRIM(NormalizedName) != ''
    UNION ALL
    SELECT PatientId,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), 1, INSTR(TRIM(str), ' ') - 1)
                ELSE TRIM(str) END,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), INSTR(TRIM(str), ' ') + 1)
                ELSE '' END
    FROM split_patient_names
    WHERE str != ''
)
INSERT INTO PatientSearchTokens (PatientId, Token, TokenType)
SELECT DISTINCT PatientId, word, 1
FROM split_patient_names
WHERE word != '';
");

            migrationBuilder.Sql(@"
INSERT INTO PatientSearchTokens (PatientId, Token, TokenType)
SELECT Id, LOWER(RecordNumber), 3
FROM Patients
WHERE RecordNumber IS NOT NULL AND RecordNumber != '';
");

            migrationBuilder.Sql(@"
WITH RECURSIVE split_med_names(MedicineId, word, str) AS (
    SELECT Id,
           CASE WHEN INSTR(TRIM(NormalizedName), ' ') > 0
                THEN SUBSTR(TRIM(NormalizedName), 1, INSTR(TRIM(NormalizedName), ' ') - 1)
                ELSE TRIM(NormalizedName) END,
           CASE WHEN INSTR(TRIM(NormalizedName), ' ') > 0
                THEN SUBSTR(TRIM(NormalizedName), INSTR(TRIM(NormalizedName), ' ') + 1)
                ELSE '' END
    FROM Medicines
    WHERE NormalizedName IS NOT NULL AND TRIM(NormalizedName) != ''
    UNION ALL
    SELECT MedicineId,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), 1, INSTR(TRIM(str), ' ') - 1)
                ELSE TRIM(str) END,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), INSTR(TRIM(str), ' ') + 1)
                ELSE '' END
    FROM split_med_names
    WHERE str != ''
)
INSERT INTO MedicineSearchTokens (MedicineId, Token, TokenType)
SELECT DISTINCT MedicineId, word, 4
FROM split_med_names
WHERE word != '';
");

            migrationBuilder.Sql(@"
INSERT OR REPLACE INTO AppMetas (Key, Value, UpdatedAtUtc)
VALUES ('SearchIndexVersion', '1', datetime('now'));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppMetas");

            migrationBuilder.DropTable(
                name: "MedicineSearchTokens");

            migrationBuilder.DropTable(
                name: "PatientSearchTokens");
        }
    }
}
