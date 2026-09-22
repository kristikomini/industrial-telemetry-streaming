using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Data;
using MotorValley.Backend.Models;
using Xunit;

namespace MotorValley.Tests;

public class MachineNoteRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;

    public MachineNoteRepositoryTests()
    {
        // A fresh, uniquely named in-memory database per test, so tests never share data.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAsync_PersistsNote()
    {
        var repo = new MachineNoteRepository(_db);
        var note = new MachineNote
        {
            MachineId = "M-001",
            Text = "Replaced coolant pump",
            Author = "Kristi"
        };

        await repo.AddAsync(note);

        var saved = await _db.MachineNotes.FirstOrDefaultAsync(n => n.MachineId == "M-001");
        Assert.NotNull(saved);
        Assert.Equal("Replaced coolant pump", saved.Text);
        Assert.Equal("Kristi", saved.Author);
    }

    [Fact]
    public async Task GetByMachineAsync_ReturnsEmpty_WhenNoNotesExist()
    {
        var repo = new MachineNoteRepository(_db);

        var notes = await repo.GetByMachineAsync("M-999");

        Assert.Empty(notes);
    }

    [Fact]
    public async Task GetByMachineAsync_ReturnsOnlyThatMachine_NewestFirst()
    {
        var repo = new MachineNoteRepository(_db);
        var now = DateTime.UtcNow;

        await repo.AddAsync(new MachineNote { MachineId = "M-001", Text = "older", CreatedAt = now.AddHours(-2) });
        await repo.AddAsync(new MachineNote { MachineId = "M-001", Text = "newer", CreatedAt = now });
        await repo.AddAsync(new MachineNote { MachineId = "M-002", Text = "other machine", CreatedAt = now });

        var notes = await repo.GetByMachineAsync("M-001");

        Assert.Equal(2, notes.Count);
        Assert.Equal("newer", notes[0].Text);
        Assert.Equal("older", notes[1].Text);
    }
}
