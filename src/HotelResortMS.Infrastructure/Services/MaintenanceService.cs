using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IMaintenanceService"/>
public class MaintenanceService : IMaintenanceService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IRoomService _roomService;
    private readonly IHousekeepingService _housekeepingService;
    private readonly IAuditService _auditService;

    public MaintenanceService(
        ApplicationDbContext db,
        INumberingService numberingService,
        IBusinessDateService businessDateService,
        IRoomService roomService,
        IHousekeepingService housekeepingService,
        IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _roomService = roomService;
        _housekeepingService = housekeepingService;
        _auditService = auditService;
    }

    public async Task<MaintenanceRequest> CreateRequestAsync(int categoryId, int? roomId, int? equipmentId, string description, string reportedBy)
    {
        var businessDate = await _businessDateService.GetCurrentAsync();

        var request = new MaintenanceRequest
        {
            RequestNumber = await _numberingService.GenerateAsync("Maintenance"),
            MaintenanceCategoryId = categoryId,
            RoomId = roomId,
            EquipmentId = equipmentId,
            Description = description,
            Status = MaintenanceRequestStatus.Open,
            ReportedBy = reportedBy,
            ReportedAt = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            CreatedBy = reportedBy
        };

        _db.MaintenanceRequests.Add(request);
        await _db.SaveChangesAsync();

        // Section 13/30: filing a request against a room takes it out of service
        // immediately - it must not remain bookable while someone is fixing it.
        if (roomId is not null)
        {
            await _roomService.SetStatusAsync(roomId.Value, RoomStatus.Maintenance, reportedBy, $"Maintenance request {request.RequestNumber} filed");
        }

        await _auditService.LogAsync(SystemModules.Maintenance, "Create", request.Id.ToString(), newValues: new { request.RequestNumber, description });
        return request;
    }

    public async Task AssignAsync(int requestId, string assignedToUserId, string assignedBy)
    {
        var request = await _db.MaintenanceRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException("Maintenance request not found.");

        request.AssignedToUserId = assignedToUserId;
        request.AssignedAt = DateTime.UtcNow;
        request.Status = MaintenanceRequestStatus.Assigned;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "Assign", requestId.ToString(), newValues: new { assignedToUserId });
    }

    public async Task StartAsync(int requestId, string startedBy)
    {
        var request = await _db.MaintenanceRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException("Maintenance request not found.");

        if (request.Status is not (MaintenanceRequestStatus.Open or MaintenanceRequestStatus.Assigned))
        {
            throw new InvalidOperationException($"Cannot start work on a {request.Status} request.");
        }

        request.Status = MaintenanceRequestStatus.InProgress;
        request.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "Start", requestId.ToString());
    }

    public async Task CompleteAsync(int requestId, string completedBy, decimal? cost = null, string? notes = null)
    {
        var request = await _db.MaintenanceRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException("Maintenance request not found.");

        if (request.Status is MaintenanceRequestStatus.Completed or MaintenanceRequestStatus.Cancelled)
        {
            throw new InvalidOperationException($"This request is already {request.Status}.");
        }

        request.Status = MaintenanceRequestStatus.Completed;
        request.CompletedAt = DateTime.UtcNow;
        request.Cost = cost;
        request.Notes = notes;
        await _db.SaveChangesAsync();

        if (request.RoomId is not null)
        {
            // Section 13/29/30: a room coming out of maintenance goes to Housekeeping, not
            // straight back to Available - it needs a clean before the next guest.
            await _housekeepingService.CreateTaskAsync(request.RoomId.Value, completedBy);
        }

        await _auditService.LogAsync(SystemModules.Maintenance, "Complete", requestId.ToString(), newValues: new { cost });
    }

    public async Task CancelAsync(int requestId, string reason, string cancelledBy)
    {
        var request = await _db.MaintenanceRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException("Maintenance request not found.");

        if (request.Status is MaintenanceRequestStatus.Completed or MaintenanceRequestStatus.Cancelled)
        {
            throw new InvalidOperationException($"This request is already {request.Status}.");
        }

        request.Status = MaintenanceRequestStatus.Cancelled;
        request.CancelReason = reason;
        await _db.SaveChangesAsync();

        if (request.RoomId is not null)
        {
            // No repair happened, so the room simply returns to Available - not Housekeeping.
            await _roomService.SetStatusAsync(request.RoomId.Value, RoomStatus.Available, cancelledBy, $"Maintenance request cancelled: {reason}");
        }

        await _auditService.LogAsync(SystemModules.Maintenance, "Cancel", requestId.ToString(), reason: reason);
    }
}
