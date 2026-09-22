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

    [Fact]
    public async Task GetByIdAsync_ReturnsNote_WhenExists()
    {
        var repo = new MachineNoteRepository(_db);
        var note = new MachineNote { MachineId = "M-001", Text = "find me", Author = "Kristi" };
        await repo.AddAsync(note);

        var found = await repo.GetByIdAsync(note.Id);

        Assert.NotNull(found);
        Assert.Equal("find me", found.Text);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = new MachineNoteRepository(_db);

        var found = await repo.GetByIdAsync(12345);

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = new MachineNoteRepository(_db);
        var note = new MachineNote { MachineId = "M-001", Text = "before", Author = "Kristi" };
        await repo.AddAsync(note);

        note.Text = "after";
        note.Author = "Editor";
        await repo.UpdateAsync(note);

        var saved = await _db.MachineNotes.FindAsync(note.Id);
        Assert.NotNull(saved);
        Assert.Equal("after", saved.Text);
        Assert.Equal("Editor", saved.Author);
    }

    [Fact]
    public async Task DeleteAsync_RemovesNote()
    {
        var repo = new MachineNoteRepository(_db);
        var note = new MachineNote { MachineId = "M-001", Text = "delete me", Author = "Kristi" };
        await repo.AddAsync(note);

        await repo.DeleteAsync(note);

        var saved = await _db.MachineNotes.FindAsync(note.Id);
        Assert.Null(saved);
    }
}
