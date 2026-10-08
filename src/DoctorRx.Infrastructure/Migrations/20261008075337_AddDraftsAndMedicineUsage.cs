using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftsAndMedicineUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FollowUpText",
                table: "Prescriptions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAtUtc",
                table: "Medicines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "Medicines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DraftKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drafts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_UsageCount",
                table: "Medicines",
                column: "UsageCount");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_DraftKey",
                table: "Drafts",
                column: "DraftKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_PatientId",
                table: "Drafts",
                column: "PatientId");

            // Recreate anti-tamper trigger to protect FollowUpText and ParentPrescriptionId (Amendment 2)
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_tamper;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_prescription_tamper
BEFORE UPDATE ON Prescriptions
FOR EACH ROW
BEGIN
    SELECT CASE
        WHEN OLD.Status IN (2, 3)
        THEN RAISE(ABORT, 'Prescription has reached terminal status and cannot be modified.')
    END;

    SELECT CASE
        WHEN OLD.Status = 1 AND NEW.Status NOT IN (1, 2, 3)
        THEN RAISE(ABORT, 'Invalid prescription status transition.')
    END;

    SELECT CASE
        WHEN NEW.Id != OLD.Id
          OR NEW.PrescriptionNumber != OLD.PrescriptionNumber
          OR NEW.PatientId != OLD.PatientId
          OR NEW.DoctorId != OLD.DoctorId
          OR NEW.PrescriptionDate != OLD.PrescriptionDate
          OR NEW.ParentPrescriptionId IS NOT OLD.ParentPrescriptionId
          OR NEW.Doctor_Name IS NOT OLD.Doctor_Name
          OR NEW.Doctor_Qualification IS NOT OLD.Doctor_Qualification
          OR NEW.Doctor_RegistrationNumber IS NOT OLD.Doctor_RegistrationNumber
          OR NEW.Doctor_Specialization IS NOT OLD.Doctor_Specialization
          OR NEW.Doctor_Phone IS NOT OLD.Doctor_Phone
          OR NEW.Doctor_ClinicName IS NOT OLD.Doctor_ClinicName
          OR NEW.Doctor_ClinicAddress IS NOT OLD.Doctor_ClinicAddress
          OR NEW.Doctor_ClinicPhone IS NOT OLD.Doctor_ClinicPhone
          OR NEW.Doctor_HeaderText IS NOT OLD.Doctor_HeaderText
          OR NEW.Doctor_FooterText IS NOT OLD.Doctor_FooterText
          OR NEW.Patient_Name IS NOT OLD.Patient_Name
          OR NEW.Patient_Gender != OLD.Patient_Gender
          OR NEW.Patient_AgeText IS NOT OLD.Patient_AgeText
          OR NEW.Patient_Phone IS NOT OLD.Patient_Phone
          OR NEW.Patient_KnownAllergies IS NOT OLD.Patient_KnownAllergies
          OR NEW.ChiefComplaints IS NOT OLD.ChiefComplaints
          OR NEW.BloodPressure IS NOT OLD.BloodPressure
          OR NEW.PulseRate IS NOT OLD.PulseRate
          OR NEW.Temperature IS NOT OLD.Temperature
          OR NEW.WeightKg IS NOT OLD.WeightKg
          OR NEW.ClinicalNotes IS NOT OLD.ClinicalNotes
          OR NEW.GeneralAdvice IS NOT OLD.GeneralAdvice
          OR NEW.FollowUpDate IS NOT OLD.FollowUpDate
          OR NEW.FollowUpText IS NOT OLD.FollowUpText
          OR NEW.CreatedAtUtc != OLD.CreatedAtUtc
          OR NEW.FinalizedAtUtc != OLD.FinalizedAtUtc
          OR NEW.AmendmentNumber != OLD.AmendmentNumber
        THEN RAISE(ABORT, 'Clinical and snapshot prescription fields are immutable.')
    END;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropIndex(
                name: "IX_Medicines_UsageCount",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "FollowUpText",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "LastUsedAtUtc",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "UsageCount",
                table: "Medicines");

            // Roll back trigger to baseline version (without FollowUpText)
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_tamper;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_prescription_tamper
BEFORE UPDATE ON Prescriptions
FOR EACH ROW
BEGIN
    SELECT CASE
        WHEN OLD.Status IN (2, 3)
        THEN RAISE(ABORT, 'Prescription has reached terminal status and cannot be modified.')
    END;

    SELECT CASE
        WHEN OLD.Status = 1 AND NEW.Status NOT IN (1, 2, 3)
        THEN RAISE(ABORT, 'Invalid prescription status transition.')
    END;

    SELECT CASE
        WHEN NEW.PrescriptionNumber != OLD.PrescriptionNumber
          OR NEW.PatientId != OLD.PatientId
          OR NEW.DoctorId != OLD.DoctorId
          OR NEW.PrescriptionDate != OLD.PrescriptionDate
          OR NEW.Doctor_Name != OLD.Doctor_Name
          OR NEW.Doctor_Qualification != OLD.Doctor_Qualification
          OR NEW.Doctor_RegistrationNumber != OLD.Doctor_RegistrationNumber
          OR NEW.Doctor_Specialization != OLD.Doctor_Specialization
          OR NEW.Doctor_ClinicName != OLD.Doctor_ClinicName
          OR NEW.Patient_Name != OLD.Patient_Name
          OR NEW.Patient_Gender != OLD.Patient_Gender
          OR NEW.Patient_AgeText != OLD.Patient_AgeText
          OR NEW.CreatedAtUtc != OLD.CreatedAtUtc
          OR NEW.FinalizedAtUtc != OLD.FinalizedAtUtc
          OR NEW.AmendmentNumber != OLD.AmendmentNumber
        THEN RAISE(ABORT, 'Clinical and snapshot prescription fields are immutable.')
    END;
END;
");
        }
    }
}
