using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Data;
using MotorValley.Backend.Models;
using Xunit;

namespace MotorValley.Tests;

public class AlertRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;

    public AlertRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAsync_PersistsAlert()
    {
        var repo = new AlertRepository(_db);
        var alert = new CriticalAlert
        {
            MachineId = "M-001",
            Temperature = 92.5,
            ConsecutiveCount = 3,
            Message = "CRITICAL_ALERT",
            Timestamp = DateTime.UtcNow
        };

        await repo.AddAsync(alert);

        var saved = await _db.CriticalAlerts.FirstOrDefaultAsync(a => a.MachineId == "M-001");
        Assert.NotNull(saved);
        Assert.Equal(92.5, saved.Temperature);
        Assert.Equal("CRITICAL_ALERT", saved.Message);
    }

    [Fact]
    public async Task GetTopFailingMachines_ReturnsOrderedByCount()
    {
        var repo = new AlertRepository(_db);
        for (int i = 0; i < 5; i++)
            await repo.AddAsync(new CriticalAlert { MachineId = "M-A", Temperature = 95, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = DateTime.UtcNow.AddSeconds(-i) });
        for (int i = 0; i < 2; i++)
            await repo.AddAsync(new CriticalAlert { MachineId = "M-B", Temperature = 91, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = DateTime.UtcNow.AddSeconds(-i) });

        var top = (await repo.GetTopFailingMachinesAsync(5)).ToList();

        Assert.Equal(2, top.Count);
        Assert.Equal("M-A", top[0].MachineId);
        Assert.Equal("M-B", top[1].MachineId);
    }

    [Fact]
    public async Task AddAsync_IsIdempotent_ByMachineIdAndTimestamp()
    {
        var repo = new AlertRepository(_db);
        var ts = DateTime.UtcNow;
        var first = await repo.AddAsync(new CriticalAlert { MachineId = "M-DUP", Temperature = 95, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = ts });
        var second = await repo.AddAsync(new CriticalAlert { MachineId = "M-DUP", Temperature = 95, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = ts });

        Assert.True(first);   // first write persists
        Assert.False(second); // redelivered duplicate is a no-op
        Assert.Equal(1, await _db.CriticalAlerts.CountAsync(x => x.MachineId == "M-DUP"));
    }

    [Fact]
    public async Task GetTopFailingWithCounts_ReturnsAlertCount()
    {
        var repo = new AlertRepository(_db);
        for (int i = 0; i < 3; i++)
            await repo.AddAsync(new CriticalAlert { MachineId = "M-X", Temperature = 95, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = DateTime.UtcNow.AddSeconds(-i) });

        var top = (await repo.GetTopFailingWithCountsAsync(5)).ToList();

        Assert.Single(top);
        Assert.Equal("M-X", top[0].MachineId);
        Assert.Equal(3, top[0].AlertCount);
    }

    [Fact]
    public async Task GetLatestStatus_ReturnsMostRecent()
    {
        var repo = new AlertRepository(_db);
        await repo.AddAsync(new CriticalAlert { MachineId = "M-X", Temperature = 90, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = DateTime.UtcNow.AddMinutes(-10) });
        await repo.AddAsync(new CriticalAlert { MachineId = "M-Y", Temperature = 95, ConsecutiveCount = 3, Message = "CRITICAL", Timestamp = DateTime.UtcNow });

        var latest = await repo.GetLatestStatusAsync();

        Assert.NotNull(latest);
        Assert.Equal("M-Y", latest.MachineId);
        Assert.Equal(95, latest.Temperature);
    }
}
