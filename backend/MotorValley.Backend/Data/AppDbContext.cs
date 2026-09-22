using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Models;

namespace MotorValley.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CriticalAlert> CriticalAlerts => Set<CriticalAlert>();
    public DbSet<MachineNote> MachineNotes => Set<MachineNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CriticalAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MachineId).HasMaxLength(64);
            e.Property(x => x.Message).HasMaxLength(128);
            e.HasIndex(x => x.Timestamp);
            // A processor emits at most one alert per machine per breach event, stamped
            // with a UTC timestamp — so (MachineId, Timestamp) uniquely identifies an
            // event. The unique index is the database-level guarantee that a redelivered
            // Kafka message cannot create a duplicate row (see AlertRepository.AddAsync),
            // and it also makes the "latest row per machine" join in
            // GetTopFailingWithCountsAsync unambiguous.
            e.HasIndex(x => new { x.MachineId, x.Timestamp }).IsUnique();
        });

        modelBuilder.Entity<MachineNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MachineId).HasMaxLength(64);
            e.Property(x => x.Text).HasMaxLength(512);
            e.Property(x => x.Author).HasMaxLength(64);
            e.HasIndex(x => x.MachineId);
        });
    }
}
