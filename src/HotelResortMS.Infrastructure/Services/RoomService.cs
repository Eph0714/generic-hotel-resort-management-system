using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IRoomService"/>
public class RoomService : IRoomService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public RoomService(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task SetStatusAsync(int roomId, RoomStatus status, string changedBy, string? reason = null)
    {
        var room = await _db.Rooms.FindAsync(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var oldStatus = room.Status;
        room.Status = status;
        room.UpdatedAt = DateTime.UtcNow;
        room.UpdatedBy = changedBy;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, "StatusChange", roomId.ToString(),
            oldValues: new { Status = oldStatus }, newValues: new { Status = status }, reason: reason);
    }
}
