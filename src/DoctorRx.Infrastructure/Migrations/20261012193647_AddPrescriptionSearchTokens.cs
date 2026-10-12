using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionSearchTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_PatientId",
                table: "Prescriptions");

            migrationBuilder.CreateTable(
                name: "PrescriptionSearchTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrescriptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    TokenType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionSearchTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionSearchTokens_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Date_Id",
                table: "Prescriptions",
                columns: new[] { "PrescriptionDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId_Date",
                table: "Prescriptions",
                columns: new[] { "PatientId", "PrescriptionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Status_Date",
                table: "Prescriptions",
                columns: new[] { "Status", "PrescriptionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionSearchTokens_PrescriptionId",
                table: "PrescriptionSearchTokens",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionSearchTokens_Token_PrescriptionId",
                table: "PrescriptionSearchTokens",
                columns: new[] { "Token", "PrescriptionId" });

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_Prescriptions_SnapshotName_Id ON Prescriptions (Patient_Name COLLATE NOCASE, Id);");

            // SQL Backfill for existing prescriptions (Prescription number parts, snapshot patient name words, items)
            migrationBuilder.Sql(@"
INSERT INTO PrescriptionSearchTokens (PrescriptionId, Token, TokenType)
SELECT Id, LOWER(PrescriptionNumber), 1
FROM Prescriptions
WHERE PrescriptionNumber IS NOT NULL AND TRIM(PrescriptionNumber) != '';

INSERT INTO PrescriptionSearchTokens (PrescriptionId, Token, TokenType)
SELECT Id, LOWER(SUBSTR(PrescriptionNumber, 4)), 1
FROM Prescriptions
WHERE PrescriptionNumber LIKE 'RX-%';

WITH RECURSIVE split_patient_names(PrescriptionId, word, str) AS (
    SELECT Id,
           CASE WHEN INSTR(TRIM(Patient_Name), ' ') > 0
                THEN SUBSTR(TRIM(Patient_Name), 1, INSTR(TRIM(Patient_Name), ' ') - 1)
                ELSE TRIM(Patient_Name) END,
           CASE WHEN INSTR(TRIM(Patient_Name), ' ') > 0
                THEN SUBSTR(TRIM(Patient_Name), INSTR(TRIM(Patient_Name), ' ') + 1)
                ELSE '' END
    FROM Prescriptions
    WHERE Patient_Name IS NOT NULL AND TRIM(Patient_Name) != ''
    UNION ALL
    SELECT PrescriptionId,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), 1, INSTR(TRIM(str), ' ') - 1)
                ELSE TRIM(str) END,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), INSTR(TRIM(str), ' ') + 1)
                ELSE '' END
    FROM split_patient_names
    WHERE str != ''
)
INSERT INTO PrescriptionSearchTokens (PrescriptionId, Token, TokenType)
SELECT DISTINCT PrescriptionId, LOWER(word), 2
FROM split_patient_names
WHERE word != '';

WITH RECURSIVE split_med_names(PrescriptionId, word, str) AS (
    SELECT PrescriptionId,
           CASE WHEN INSTR(TRIM(MedicineName), ' ') > 0
                THEN SUBSTR(TRIM(MedicineName), 1, INSTR(TRIM(MedicineName), ' ') - 1)
                ELSE TRIM(MedicineName) END,
           CASE WHEN INSTR(TRIM(MedicineName), ' ') > 0
                THEN SUBSTR(TRIM(MedicineName), INSTR(TRIM(MedicineName), ' ') + 1)
                ELSE '' END
    FROM PrescriptionMedicines
    WHERE MedicineName IS NOT NULL AND TRIM(MedicineName) != ''
    UNION ALL
    SELECT PrescriptionId,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), 1, INSTR(TRIM(str), ' ') - 1)
                ELSE TRIM(str) END,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), INSTR(TRIM(str), ' ') + 1)
                ELSE '' END
    FROM split_med_names
    WHERE str != ''
)
INSERT INTO PrescriptionSearchTokens (PrescriptionId, Token, TokenType)
SELECT DISTINCT PrescriptionId, LOWER(word), 3
FROM split_med_names
WHERE word != '';

WITH RECURSIVE split_gen_names(PrescriptionId, word, str) AS (
    SELECT PrescriptionId,
           CASE WHEN INSTR(TRIM(GenericName), ' ') > 0
                THEN SUBSTR(TRIM(GenericName), 1, INSTR(TRIM(GenericName), ' ') - 1)
                ELSE TRIM(GenericName) END,
           CASE WHEN INSTR(TRIM(GenericName), ' ') > 0
                THEN SUBSTR(TRIM(GenericName), INSTR(TRIM(GenericName), ' ') + 1)
                ELSE '' END
    FROM PrescriptionMedicines
    WHERE GenericName IS NOT NULL AND TRIM(GenericName) != ''
    UNION ALL
    SELECT PrescriptionId,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), 1, INSTR(TRIM(str), ' ') - 1)
                ELSE TRIM(str) END,
           CASE WHEN INSTR(TRIM(str), ' ') > 0
                THEN SUBSTR(TRIM(str), INSTR(TRIM(str), ' ') + 1)
                ELSE '' END
    FROM split_gen_names
    WHERE str != ''
)
INSERT INTO PrescriptionSearchTokens (PrescriptionId, Token, TokenType)
SELECT DISTINCT PrescriptionId, LOWER(word), 4
FROM split_gen_names
WHERE word != '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrescriptionSearchTokens");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_Date_Id",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_PatientId_Date",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_Status_Date",
                table: "Prescriptions");

            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Prescriptions_SnapshotName_Id;");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId",
                table: "Prescriptions",
                column: "PatientId");
        }
    }
}
