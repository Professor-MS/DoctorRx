using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Qualification = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Specialization = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ClinicName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClinicAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ClinicPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    HeaderText = table.Column<string>(type: "TEXT", nullable: true),
                    FooterText = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GenericName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Form = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    SequenceKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CurrentValue = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.SequenceKey);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Age = table.Column<int>(type: "INTEGER", nullable: true),
                    AgeRecordedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    PhoneDigits = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    MedicalHistoryNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    KnownAllergies = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastVisitDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrescriptionNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    DoctorId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrescriptionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Doctor_Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Doctor_Qualification = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Doctor_RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Doctor_Specialization = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Doctor_Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Doctor_ClinicName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Doctor_ClinicAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Doctor_ClinicPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Doctor_HeaderText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Doctor_FooterText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Patient_Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Patient_Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    Patient_AgeText = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Patient_Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Patient_KnownAllergies = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ChiefComplaints = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BloodPressure = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PulseRate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Temperature = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    WeightKg = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ClinicalNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    GeneralAdvice = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentPrescriptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    AmendmentNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Prescriptions_ParentPrescriptionId",
                        column: x => x.ParentPrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionMedicines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrescriptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicineId = table.Column<int>(type: "INTEGER", nullable: true),
                    MedicineName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GenericName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Form = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Dose = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timing = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MealRelation = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomMealRelationText = table.Column<string>(type: "TEXT", nullable: true),
                    WithWhat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Route = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionMedicines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionMedicines_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionMedicines_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_GenericName",
                table: "Medicines",
                column: "GenericName");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_Name",
                table: "Medicines",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_NormalizedName",
                table: "Medicines",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_IsArchived",
                table: "Patients",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Name",
                table: "Patients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NormalizedName",
                table: "Patients",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Phone",
                table: "Patients",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PhoneDigits",
                table: "Patients",
                column: "PhoneDigits");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_RecordNumber",
                table: "Patients",
                column: "RecordNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionMedicines_MedicineId",
                table: "PrescriptionMedicines",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionMedicines_PrescriptionId",
                table: "PrescriptionMedicines",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Date",
                table: "Prescriptions",
                column: "PrescriptionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_DoctorId",
                table: "Prescriptions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Number",
                table: "Prescriptions",
                column: "PrescriptionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ParentPrescriptionId",
                table: "Prescriptions",
                column: "ParentPrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId",
                table: "Prescriptions",
                column: "PatientId");

            // Anti-Tamper SQLite Triggers (Amendment 2)
            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS trg_prevent_prescription_tamper
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

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS trg_prevent_prescription_delete
BEFORE DELETE ON Prescriptions
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Prescriptions cannot be deleted.');
END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS trg_prevent_prescription_medicine_delete
BEFORE DELETE ON PrescriptionMedicines
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Prescription medicine items cannot be deleted.');
END;
");

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS trg_prevent_prescription_medicine_update
BEFORE UPDATE ON PrescriptionMedicines
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'Prescription medicine items cannot be updated.');
END;
");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_tamper;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_medicine_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_medicine_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_prevent_prescription_medicine_insert_after_terminal;");
            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "PrescriptionMedicines");

            migrationBuilder.DropTable(
                name: "Medicines");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
