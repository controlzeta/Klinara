using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentHistory> AppointmentHistory => Set<AppointmentHistory>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<ClinicHoliday> ClinicHolidays => Set<ClinicHoliday>();
    public DbSet<DoctorVacation> DoctorVacations => Set<DoctorVacation>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientToken> PatientTokens => Set<PatientToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Clinic>(entity =>
        {
            entity.HasIndex(e => e.Subdomain).IsUnique();
            entity.HasIndex(e => e.ContactEmail).IsUnique();
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
        });

        // Appointment
        builder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.StartAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.ClinicId, e.StartAt });
            entity.HasIndex(e => new { e.DoctorId, e.StartAt });
            entity.HasOne(e => e.Doctor)
                .WithMany()
                .HasForeignKey(e => e.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CancelledByUser)
                .WithMany()
                .HasForeignKey(e => e.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AppointmentHistory
        builder.Entity<AppointmentHistory>(entity =>
        {
            entity.HasIndex(e => e.AppointmentId);
            entity.HasIndex(e => e.At);
            entity.HasOne(e => e.Appointment)
                .WithMany(a => a.History)
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChangedByUser)
                .WithMany()
                .HasForeignKey(e => e.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // DoctorSchedule
        builder.Entity<DoctorSchedule>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => e.DayOfWeek);
            entity.HasIndex(e => new { e.DoctorId, e.DayOfWeek, e.EffectiveFrom, e.EffectiveTo });
            entity.HasOne(e => e.Doctor)
                .WithMany()
                .HasForeignKey(e => e.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TimeSlot
                builder.Entity<TimeSlot>(entity =>
                {
                    entity.HasIndex(e => e.ClinicId);
                    entity.HasIndex(e => e.DoctorId);
                    entity.HasIndex(e => e.StartAt);
                    entity.HasIndex(e => e.IsBooked);
                    entity.HasIndex(e => new { e.DoctorId, e.StartAt, e.EndAt });
                    entity.HasOne(e => e.Doctor)
                        .WithMany()
                        .HasForeignKey(e => e.DoctorId)
                        .OnDelete(DeleteBehavior.Restrict);
                    entity.HasOne(e => e.Appointment)
                        .WithMany()
                        .HasForeignKey(e => e.AppointmentId)
                        .OnDelete(DeleteBehavior.SetNull);
                    entity.HasOne(e => e.Clinic)
                        .WithMany()
                        .HasForeignKey(e => e.ClinicId)
                        .OnDelete(DeleteBehavior.NoAction);
                });

        // ClinicHoliday
        builder.Entity<ClinicHoliday>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.ClinicId, e.Date });
        });

        // DoctorVacation
        builder.Entity<DoctorVacation>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => new { e.DoctorId, e.StartDate, e.EndDate });
            entity.HasOne(e => e.Doctor)
                .WithMany()
                .HasForeignKey(e => e.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Patient
        builder.Entity<Patient>(entity =>
        {
            entity.HasIndex(e => e.ClinicId);
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Phone);
            entity.HasIndex(e => new { e.ClinicId, e.Email });
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PatientToken
                        builder.Entity<PatientToken>(entity =>
                        {
                            entity.HasIndex(e => e.PatientId);
                            entity.HasIndex(e => e.ClinicId);
                                                        entity.HasIndex(e => e.Token).IsUnique();
                            entity.HasIndex(e => e.ExpiresAt);
                            entity.HasIndex(e => e.AppointmentId);
                            entity.HasOne(e => e.Patient)
                                .WithMany(p => p.Tokens)
                                .HasForeignKey(e => e.PatientId)
                                .OnDelete(DeleteBehavior.NoAction);
                            entity.HasOne(e => e.Clinic)
                                .WithMany()
                                .HasForeignKey(e => e.ClinicId)
                                .OnDelete(DeleteBehavior.NoAction);
                            entity.HasOne(e => e.Appointment)
                                .WithMany()
                                .HasForeignKey(e => e.AppointmentId)
                                .OnDelete(DeleteBehavior.SetNull);
                        });
    }
}
