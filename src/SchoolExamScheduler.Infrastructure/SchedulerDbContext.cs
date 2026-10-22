using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolExamScheduler.Domain;

namespace SchoolExamScheduler.Infrastructure;

public sealed class SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : DbContext(options)
{
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherAvailability> Availability => Set<TeacherAvailability>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueUnavailability> VenueUnavailability => Set<VenueUnavailability>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamVenue> ExamVenues => Set<ExamVenue>();
    public DbSet<InvigilationAssignment> Assignments => Set<InvigilationAssignment>();
    public DbSet<RosterVersion> Rosters => Set<RosterVersion>();
    public DbSet<SchedulingSettings> Settings => Set<SchedulingSettings>();
    public DbSet<AuditRecord> Audit => Set<AuditRecord>();
    public static SchedulerDbContext Open(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var connection=new SqliteConnectionStringBuilder { DataSource=databasePath,ForeignKeys=true,DefaultTimeout=30 };
        return new(new DbContextOptionsBuilder<SchedulerDbContext>().UseSqlite(connection.ConnectionString).Options);
    }
    public async Task InitializeAsync(CancellationToken cancellationToken=default)
    {
        await Database.MigrateAsync(cancellationToken);
        if(!await Settings.AnyAsync(cancellationToken))
        {
            Settings.Add(new SchedulingSettings());await SaveChangesAsync(cancellationToken);
        }
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateConversion>();
        configurationBuilder.Properties<TimeOnly>().HaveConversion<TimeConversion>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<InstantConversion>();
    }
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Teacher>().Ignore(x=>x.IsEligible);
        b.Entity<Teacher>().HasIndex(x=>x.EmployeeNumber).IsUnique();
        b.Entity<Teacher>().ToTable("Teachers", t=>
        {
            t.HasCheckConstraint("CK_Teacher_Weight","WorkloadWeight > 0");
            t.HasCheckConstraint("CK_Teacher_Daily","MaximumDailyMinutes IS NULL OR MaximumDailyMinutes > 0");
            t.HasCheckConstraint("CK_Teacher_Period","MaximumPeriodMinutes IS NULL OR MaximumPeriodMinutes > 0");
        });
        b.Entity<Subject>().HasIndex(x=>x.SubjectCode).IsUnique();
        b.Entity<Venue>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<Venue>().ToTable("Venues",t=>t.HasCheckConstraint("CK_Venue_Capacity","Capacity > 0"));
        b.Entity<Exam>().Ignore(x=>x.Interval);
        b.Entity<Exam>().HasOne<Subject>().WithMany().HasForeignKey(x=>x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Exam>().HasIndex(x=>new {x.ExamDate,x.StartTime});
        b.Entity<Exam>().ToTable("Exams",t=>
        {
            t.HasCheckConstraint("CK_Exam_Time","StartTime < EndTime");t.HasCheckConstraint("CK_Exam_Grade","Grade > 0");
        });
        b.Entity<TeacherAvailability>().Ignore(x=>x.Interval);
        b.Entity<TeacherAvailability>().HasOne<Teacher>().WithMany().HasForeignKey(x=>x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TeacherAvailability>().HasIndex(x=>new{x.TeacherId,x.Date});
        b.Entity<TeacherAvailability>().ToTable("Availability",t=>t.HasCheckConstraint("CK_Availability_Time","StartTime < EndTime"));
        b.Entity<VenueUnavailability>().Ignore(x=>x.Interval);
        b.Entity<VenueUnavailability>().HasOne<Venue>().WithMany().HasForeignKey(x=>x.VenueId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VenueUnavailability>().ToTable("VenueUnavailability",t=>t.HasCheckConstraint("CK_VenueAvailability_Time","StartTime < EndTime"));
        b.Entity<ExamVenue>().HasOne<Exam>().WithMany().HasForeignKey(x=>x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExamVenue>().HasOne<Venue>().WithMany().HasForeignKey(x=>x.VenueId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExamVenue>().HasIndex(x=>new{x.ExamId,x.VenueId}).IsUnique();
        b.Entity<ExamVenue>().ToTable("ExamVenues",t=>
        {
            t.HasCheckConstraint("CK_ExamVenue_Students","ExpectedStudentCount > 0");
            t.HasCheckConstraint("CK_ExamVenue_Positions","RequiredInvigilatorCount > 0");
        });
        b.Entity<InvigilationAssignment>().Ignore(x=>x.Interval);
        b.Entity<InvigilationAssignment>().HasOne<Teacher>().WithMany().HasForeignKey(x=>x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InvigilationAssignment>().HasOne<ExamVenue>().WithMany().HasForeignKey(x=>x.ExamVenueId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InvigilationAssignment>().HasOne<RosterVersion>().WithMany().HasForeignKey(x=>x.RosterVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InvigilationAssignment>().HasIndex(x=>new{x.RosterVersionId,x.ExamVenueId,x.PositionIndex,x.StartTime}).IsUnique();
        b.Entity<InvigilationAssignment>().ToTable("Assignments",t=>
        {
            t.HasCheckConstraint("CK_Assignment_Time","StartTime < EndTime");t.HasCheckConstraint("CK_Assignment_Position","PositionIndex >= 0");
        });
        b.Entity<RosterVersion>().ToTable("Rosters");
        b.Entity<SchedulingSettings>().ToTable("Settings",t=>t.HasCheckConstraint("CK_Settings_Singleton","Id = 1"));
        b.Entity<AuditRecord>().ToTable("Audit");
    }
    public sealed class DateConversion() : ValueConverter<DateOnly,int>(x=>x.DayNumber,x=>DateOnly.FromDayNumber(x));
    public sealed class TimeConversion() : ValueConverter<TimeOnly,long>(x=>x.Ticks,x=>new TimeOnly(x));
    public sealed class InstantConversion() : ValueConverter<DateTimeOffset,long>(x=>x.UtcTicks,x=>new DateTimeOffset(x,TimeSpan.Zero));
}
