using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionIsSealed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSealed",
                table: "Prescriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            // Resolve any pre-existing duplicate active doctors before creating the unique filtered index
            migrationBuilder.Sql(@"
UPDATE Doctors 
SET IsActive = 0 
WHERE IsActive = 1 
  AND Id NOT IN (
    SELECT Id FROM Doctors WHERE IsActive = 1 ORDER BY Id ASC LIMIT 1
  );
");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_SingleActive",
                table: "Doctors",
                column: "IsActive",
                unique: true,
                filter: "IsActive = 1");

            // Recreate anti-tamper triggers with IsSealed protection
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
        WHEN OLD.IsSealed = 1 AND NEW.IsSealed != 1
        THEN RAISE(ABORT, 'Sealed prescription cannot be unsealed.')
    END;

    SELECT CASE
        WHEN (OLD.Status = 1 OR OLD.IsSealed = 1) AND (
             NEW.Id != OLD.Id
          OR NEW.PrescriptionNumber != OLD.PrescriptionNumber
          OR NEW.PatientId != OLD.PatientId
          OR NEW.DoctorId != OLD.DoctorId
          OR NEW.PrescriptionDate != OLD.PrescriptionDate
          OR NEW.ParentPrescriptionId IS NOT OLD.ParentPrescriptionId
          OR NEW.Doctor_TitlePrefix IS NOT OLD.Doctor_TitlePrefix
          OR NEW.Doctor_Name IS NOT OLD.Doctor_Name
          OR NEW.Doctor_Qualification IS NOT OLD.Doctor_Qualification
          OR NEW.Doctor_RegistrationLabel IS NOT OLD.Doctor_RegistrationLabel
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
        )
        THEN RAISE(ABORT, 'Clinical and snapshot prescription fields are immutable.')
    END;
END;
");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_medicine_insert_after_terminal;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_prevent_prescription_medicine_insert_after_terminal
BEFORE INSERT ON PrescriptionMedicines
FOR EACH ROW
WHEN (SELECT IsSealed FROM Prescriptions WHERE Id = NEW.PrescriptionId) = 1 
  OR (SELECT Status FROM Prescriptions WHERE Id = NEW.PrescriptionId) IN (2, 3)
BEGIN
    SELECT RAISE(ABORT, 'Cannot add medicine items to a sealed, cancelled or superseded prescription.');
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Doctors_SingleActive",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "IsSealed",
                table: "Prescriptions");

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
          OR NEW.Doctor_TitlePrefix IS NOT OLD.Doctor_TitlePrefix
          OR NEW.Doctor_Name IS NOT OLD.Doctor_Name
          OR NEW.Doctor_Qualification IS NOT OLD.Doctor_Qualification
          OR NEW.Doctor_RegistrationLabel IS NOT OLD.Doctor_RegistrationLabel
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

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_medicine_insert_after_terminal;");
            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS trg_prevent_prescription_medicine_insert_after_terminal
BEFORE INSERT ON PrescriptionMedicines
FOR EACH ROW
WHEN (SELECT Status FROM Prescriptions WHERE Id = NEW.PrescriptionId) IN (2, 3)
BEGIN
    SELECT RAISE(ABORT, 'Cannot add medicine items to a cancelled or superseded prescription.');
END;
");
        }
    }
}
