using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IHousekeepingService"/>
public class HousekeepingService : IHousekeepingService
{
    private readonly ApplicationDbContext _db;
    private readonly IRoomService _roomService;
    private readonly IAuditService _auditService;

    public HousekeepingService(ApplicationDbContext db, IRoomService roomService, IAuditService auditService)
    {
        _db = db;
        _roomService = roomService;
        _auditService = auditService;
    }

    public async Task<HousekeepingTask> CreateTaskAsync(int roomId, string createdBy, string? assignedToUserId = null)
    {
        var openTaskExists = await _db.HousekeepingTasks
            .AnyAsync(t => t.RoomId == roomId && t.Status != HousekeepingStatus.Ready);
        if (openTaskExists)
        {
            throw new InvalidOperationException("This room already has an open housekeeping task.");
        }

        var task = new HousekeepingTask
        {
            RoomId = roomId,
            Status = HousekeepingStatus.Dirty,
            AssignedToUserId = assignedToUserId,
            AssignedAt = assignedToUserId is not null ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.HousekeepingTasks.Add(task);
        await _db.SaveChangesAsync();

        await _roomService.SetStatusAsync(roomId, RoomStatus.Dirty, createdBy, "Housekeeping task created");
        await _auditService.LogAsync(SystemModules.Housekeeping, "Create", task.Id.ToString(), newValues: new { roomId });

        return task;
    }

    public async Task AssignAsync(int taskId, string assignedToUserId, string assignedBy)
    {
        var task = await _db.HousekeepingTasks.FindAsync(taskId)
            ?? throw new InvalidOperationException("Housekeeping task not found.");

        task.AssignedToUserId = assignedToUserId;
        task.AssignedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Housekeeping, "Assign", taskId.ToString(), newValues: new { assignedToUserId });
    }

    public async Task StartCleaningAsync(int taskId, string startedBy)
    {
        var task = await _db.HousekeepingTasks.FindAsync(taskId)
            ?? throw new InvalidOperationException("Housekeeping task not found.");

        if (task.Status != HousekeepingStatus.Dirty)
        {
            throw new InvalidOperationException($"Only a Dirty task can start cleaning (current status: {task.Status}).");
        }

        task.Status = HousekeepingStatus.Cleaning;
        task.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _roomService.SetStatusAsync(task.RoomId, RoomStatus.Cleaning, startedBy);
        await _auditService.LogAsync(SystemModules.Housekeeping, "StartCleaning", taskId.ToString());
    }

    public async Task CompleteCleaningAsync(int taskId, string completedBy, string? notes = null)
    {
        var task = await _db.HousekeepingTasks.FindAsync(taskId)
            ?? throw new InvalidOperationException("Housekeeping task not found.");

        if (task.Status != HousekeepingStatus.Cleaning)
        {
            throw new InvalidOperationException($"Only a Cleaning task can be completed (current status: {task.Status}).");
        }

        task.Status = HousekeepingStatus.Clean;
        task.CompletedAt = DateTime.UtcNow;
        task.Notes = notes;
        await _db.SaveChangesAsync();

        await _roomService.SetStatusAsync(task.RoomId, RoomStatus.Clean, completedBy);
        await _auditService.LogAsync(SystemModules.Housekeeping, "CompleteCleaning", taskId.ToString());
    }

    public async Task InspectAsync(int taskId, string inspectedBy, bool passed, string? notes = null)
    {
        var task = await _db.HousekeepingTasks.FindAsync(taskId)
            ?? throw new InvalidOperationException("Housekeeping task not found.");

        if (task.Status != HousekeepingStatus.Clean)
        {
            throw new InvalidOperationException($"Only a Clean task can be inspected (current status: {task.Status}).");
        }

        task.InspectedByUserId = inspectedBy;
        task.InspectedAt = DateTime.UtcNow;
        task.Notes = notes;

        if (passed)
        {
            task.Status = HousekeepingStatus.Ready;
            await _db.SaveChangesAsync();

            // Section 13: an inspected-and-passed room is finally available to book again.
            await _roomService.SetStatusAsync(task.RoomId, RoomStatus.Available, inspectedBy, "Passed housekeeping inspection");
        }
        else
        {
            // Section 29: a failed inspection reopens the same task rather than creating a
            // new one - the full history (who cleaned it, who failed it, why) stays together.
            task.Status = HousekeepingStatus.Dirty;
            task.StartedAt = null;
            task.CompletedAt = null;
            await _db.SaveChangesAsync();

            await _roomService.SetStatusAsync(task.RoomId, RoomStatus.Dirty, inspectedBy, $"Failed inspection: {notes}");
        }

        await _auditService.LogAsync(SystemModules.Housekeeping, "Inspect", taskId.ToString(), newValues: new { passed }, reason: notes);
    }
}
