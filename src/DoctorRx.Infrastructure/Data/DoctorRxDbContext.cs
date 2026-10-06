using System;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Common;
using DoctorRx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Data;

public class DoctorRxDbContext : DbContext
{
    public DoctorRxDbContext(DbContextOptions<DoctorRxDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionMedicine> PrescriptionMedicines => Set<PrescriptionMedicine>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Patient configuration
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecordNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.RecordNumber).IsUnique().HasDatabaseName("IX_Patients_RecordNumber");

            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(150).UseCollation("NOCASE");
            entity.HasIndex(e => e.NormalizedName).HasDatabaseName("IX_Patients_NormalizedName");

            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.PhoneDigits).HasMaxLength(30);
            entity.HasIndex(e => e.PhoneDigits).HasDatabaseName("IX_Patients_PhoneDigits");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.HasIndex(e => e.IsArchived).HasDatabaseName("IX_Patients_IsArchived");

            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.MedicalHistoryNotes).HasMaxLength(1000);
            entity.Property(e => e.KnownAllergies).HasMaxLength(500);

            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Patients_Name");
            entity.HasIndex(e => e.Phone).HasDatabaseName("IX_Patients_Phone");
        });

        // Doctor configuration
        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.ToTable("Doctors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Qualification).IsRequired().HasMaxLength(150);
            entity.Property(e => e.RegistrationNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specialization).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ClinicName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ClinicAddress).HasMaxLength(300);
            entity.Property(e => e.ClinicPhone).HasMaxLength(30);
            entity.Property(e => e.Email).HasMaxLength(100);
        });

        // Medicine catalog configuration (Zero clinical defaults)
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.ToTable("Medicines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(150).UseCollation("NOCASE");
            entity.Property(e => e.GenericName).HasMaxLength(150).UseCollation("NOCASE");
            entity.Property(e => e.Form).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Strength).HasMaxLength(50);

            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Medicines_Name");
            entity.HasIndex(e => e.NormalizedName).HasDatabaseName("IX_Medicines_NormalizedName");
            entity.HasIndex(e => e.GenericName).HasDatabaseName("IX_Medicines_GenericName");
        });

        // Prescription configuration
        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.ToTable("Prescriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrescriptionNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.PrescriptionNumber).IsUnique().HasDatabaseName("IX_Prescriptions_Number");
            entity.HasIndex(e => e.PrescriptionDate).HasDatabaseName("IX_Prescriptions_Date");

            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.CancellationReason).HasMaxLength(500);

            entity.Property(e => e.ChiefComplaints).HasMaxLength(1000);
            entity.Property(e => e.BloodPressure).HasMaxLength(20);
            entity.Property(e => e.PulseRate).HasMaxLength(20);
            entity.Property(e => e.Temperature).HasMaxLength(20);
            entity.Property(e => e.WeightKg).HasMaxLength(20);
            entity.Property(e => e.ClinicalNotes).HasMaxLength(2000);
            entity.Property(e => e.GeneralAdvice).HasMaxLength(2000);

            // Owned Doctor Snapshot
            entity.OwnsOne(e => e.DoctorSnapshot, d =>
            {
                d.Property(x => x.Name).HasColumnName("Doctor_Name").IsRequired().HasMaxLength(150);
                d.Property(x => x.Qualification).HasColumnName("Doctor_Qualification").IsRequired().HasMaxLength(150);
                d.Property(x => x.RegistrationNumber).HasColumnName("Doctor_RegistrationNumber").IsRequired().HasMaxLength(50);
                d.Property(x => x.Specialization).HasColumnName("Doctor_Specialization").IsRequired().HasMaxLength(150);
                d.Property(x => x.Phone).HasColumnName("Doctor_Phone").HasMaxLength(30);
                d.Property(x => x.ClinicName).HasColumnName("Doctor_ClinicName").IsRequired().HasMaxLength(200);
                d.Property(x => x.ClinicAddress).HasColumnName("Doctor_ClinicAddress").HasMaxLength(300);
                d.Property(x => x.ClinicPhone).HasColumnName("Doctor_ClinicPhone").HasMaxLength(30);
                d.Property(x => x.HeaderText).HasColumnName("Doctor_HeaderText").HasMaxLength(500);
                d.Property(x => x.FooterText).HasColumnName("Doctor_FooterText").HasMaxLength(500);
            });

            // Owned Patient Snapshot
            entity.OwnsOne(e => e.PatientSnapshot, p =>
            {
                p.Property(x => x.Name).HasColumnName("Patient_Name").IsRequired().HasMaxLength(150);
                p.Property(x => x.Gender).HasColumnName("Patient_Gender").IsRequired();
                p.Property(x => x.AgeText).HasColumnName("Patient_AgeText").IsRequired().HasMaxLength(50);
                p.Property(x => x.Phone).HasColumnName("Patient_Phone").HasMaxLength(30);
                p.Property(x => x.KnownAllergies).HasColumnName("Patient_KnownAllergies").HasMaxLength(500);
            });

            // Relationships
            entity.HasOne(e => e.Patient)
                  .WithMany(p => p.Prescriptions)
                  .HasForeignKey(e => e.PatientId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Doctor)
                  .WithMany(d => d.Prescriptions)
                  .HasForeignKey(e => e.DoctorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParentPrescription)
                  .WithMany()
                  .HasForeignKey(e => e.ParentPrescriptionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Prescription)
                  .HasForeignKey(i => i.PrescriptionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PrescriptionMedicine configuration (Immutable Snapshot Rule)
        modelBuilder.Entity<PrescriptionMedicine>(entity =>
        {
            entity.ToTable("PrescriptionMedicines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MedicineName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.GenericName).HasMaxLength(150);
            entity.Property(e => e.Form).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Strength).HasMaxLength(50);
            entity.Property(e => e.Dose).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Frequency).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Timing).HasMaxLength(100);
            entity.Property(e => e.WithWhat).HasMaxLength(100);
            entity.Property(e => e.Route).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Duration).HasMaxLength(50);
            entity.Property(e => e.Instructions).HasMaxLength(500);

            // Foreign key to medicine catalog with Restrict delete behavior (Amendment 2a)
            entity.HasOne<Medicine>()
                  .WithMany()
                  .HasForeignKey(e => e.MedicineId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // NumberSequence configuration
        modelBuilder.Entity<NumberSequence>(entity =>
        {
            entity.ToTable("NumberSequences");
            entity.HasKey(e => e.SequenceKey);
            entity.Property(e => e.SequenceKey).HasMaxLength(50);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
