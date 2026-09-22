using MotorValley.Backend.Models;

namespace MotorValley.Backend.Data;

public interface IMachineNoteRepository
{
    Task AddAsync(MachineNote note);
    Task<List<MachineNote>> GetByMachineAsync(string machineId);
    Task<MachineNote?> GetByIdAsync(int id);
    Task UpdateAsync(MachineNote note);
    Task DeleteAsync(MachineNote note);
}
