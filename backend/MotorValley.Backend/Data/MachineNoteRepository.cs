using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Models;

namespace MotorValley.Backend.Data;

public class MachineNoteRepository : IMachineNoteRepository
{
    private readonly AppDbContext _db;

    public MachineNoteRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(MachineNote note)
    {
        _db.MachineNotes.Add(note);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MachineNote>> GetByMachineAsync(string machineId)
    {
        return await _db.MachineNotes
            .Where(n => n.MachineId == machineId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<MachineNote?> GetByIdAsync(int id)
    {
        return await _db.MachineNotes.FindAsync(id);
    }

    public async Task UpdateAsync(MachineNote note)
    {
        _db.MachineNotes.Update(note);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(MachineNote note)
    {
        _db.MachineNotes.Remove(note);
        await _db.SaveChangesAsync();
    }
}
