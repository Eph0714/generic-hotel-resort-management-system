using System.Text.Json;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IAuditService"/>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessDateService _businessDateService;

    public AuditService(ApplicationDbContext db, ICurrentUserService currentUser, IBusinessDateService businessDateService)
    {
        _db = db;
        _currentUser = currentUser;
        _businessDateService = businessDateService;
    }

    public async Task LogAsync(
        string module,
        string action,
        string? recordId = null,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null)
    {
        // Every audit row is stamped with both the actual timestamp and the hotel's
        // operating Business Date (Section 12/46) - the two can differ for late-night
        // actions taken before Night Audit rolls the day forward.
        var businessDate = await _businessDateService.GetCurrentAsync();

        var log = new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Module = module,
            Action = action,
            RecordId = recordId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Reason = reason,
            IpAddress = _currentUser.IpAddress,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}
