using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorTitleAndRegistrationLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Doctor_RegistrationLabel",
                table: "Prescriptions",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Reg. No.");

            migrationBuilder.AddColumn<string>(
                name: "Doctor_TitlePrefix",
                table: "Prescriptions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dr.");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationLabel",
                table: "Doctors",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Reg. No.");

            migrationBuilder.AddColumn<string>(
                name: "TitlePrefix",
                table: "Doctors",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dr.");

            // Recreate anti-tamper trigger to protect Doctor_TitlePrefix and Doctor_RegistrationLabel
            // Derived from latest trigger definition in 20261008075337_AddDraftsAndMedicineUsage.cs
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Doctor_RegistrationLabel",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Doctor_TitlePrefix",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "RegistrationLabel",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "TitlePrefix",
                table: "Doctors");
        }
    }
}
