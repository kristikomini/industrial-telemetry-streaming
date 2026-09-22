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
}
