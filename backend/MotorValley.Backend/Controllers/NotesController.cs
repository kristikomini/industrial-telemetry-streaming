using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MotorValley.Backend.Data;
using MotorValley.Backend.Models;

namespace MotorValley.Backend.Controllers
{
    
[ApiController]
[Route("api/machines/{machineId}/notes")]
public class NotesController : ControllerBase
{

    private readonly IMachineNoteRepository _machineNoteRepository;

    public NotesController(IMachineNoteRepository machineNoteRepository)
    {
        _machineNoteRepository = machineNoteRepository;
    }

    [HttpGet]
public async Task<IActionResult> GetNotes(string machineId)
{
    var notes = await _machineNoteRepository.GetByMachineAsync(machineId);
    return Ok(notes);
}

    [HttpPost]
public async Task<IActionResult> AddNote(string machineId, [FromBody] MachineNoteDto dto)
{
    var note = new MachineNote
    {
        MachineId = machineId,
        Text = dto.Text,
        Author = dto.Author
    };

    await _machineNoteRepository.AddAsync(note);
    return CreatedAtAction(nameof(GetNotes), new { machineId }, note);
}

}

}