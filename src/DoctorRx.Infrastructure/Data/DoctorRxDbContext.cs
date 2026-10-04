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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Patient configuration
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.MedicalHistoryNotes).HasMaxLength(1000);
            entity.Property(e => e.KnownAllergies).HasMaxLength(500);

            // Indexes for fast searching across thousands of patients
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

        // Medicine catalog configuration
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.ToTable("Medicines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.GenericName).HasMaxLength(150);
            entity.Property(e => e.Form).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Strength).HasMaxLength(50);
            entity.Property(e => e.DefaultDose).HasMaxLength(50);
            entity.Property(e => e.DefaultFrequency).HasMaxLength(50);
            entity.Property(e => e.DefaultRoute).HasMaxLength(50);

            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Medicines_Name");
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

            entity.Property(e => e.ChiefComplaints).HasMaxLength(1000);
            entity.Property(e => e.BloodPressure).HasMaxLength(20);
            entity.Property(e => e.PulseRate).HasMaxLength(20);
            entity.Property(e => e.Temperature).HasMaxLength(20);
            entity.Property(e => e.WeightKg).HasMaxLength(20);
            entity.Property(e => e.ClinicalNotes).HasMaxLength(2000);
            entity.Property(e => e.GeneralAdvice).HasMaxLength(2000);

            // Relationships
            entity.HasOne(e => e.Patient)
                  .WithMany(p => p.Prescriptions)
                  .HasForeignKey(e => e.PatientId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Doctor)
                  .WithMany(d => d.Prescriptions)
                  .HasForeignKey(e => e.DoctorId)
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
            entity.Property(e => e.Route).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Duration).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Instructions).HasMaxLength(500);

            // Foreign key to medicine catalog is optional; if deleted or changed in catalog, prescription record is NOT altered.
            entity.HasOne<Medicine>()
                  .WithMany()
                  .HasForeignKey(e => e.MedicineId)
                  .OnDelete(DeleteBehavior.SetNull);
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
