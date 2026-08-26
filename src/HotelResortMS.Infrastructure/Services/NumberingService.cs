using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="INumberingService"/>
public class NumberingService : INumberingService
{
    private readonly ApplicationDbContext _db;

    // Default prefixes per Section 47; DocumentNumberCounter rows can override PaddingWidth
    // later without a code change, but the prefix mapping itself starts here.
    private static readonly Dictionary<string, string> DefaultPrefixes = new()
    {
        ["Reservation"] = "RES",
        ["Folio"] = "FOL",
        ["POS"] = "POS",
        ["Payment"] = "PAY",
        ["Expense"] = "EXP",
        ["Purchase"] = "PUR",
        ["Income"] = "INC",
        ["Event"] = "EVT",
        ["Audit"] = "AUD"
    };

    public NumberingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(string documentType)
    {
        var year = DateTime.UtcNow.Year;

        // Serialize concurrent number allocation for the same (type, year) via a DB
        // transaction so two simultaneous requests can never receive the same number.
        // EF Core does not support nested transactions on one connection - if the caller
        // (e.g. ReservationService, FrontDeskService) already has one open, join it instead
        // of starting a second one, and let the caller own the commit/rollback.
        var ambientTransaction = _db.Database.CurrentTransaction;
        IDbContextTransaction? ownTransaction = null;
        if (ambientTransaction is null)
        {
            ownTransaction = await _db.Database.BeginTransactionAsync();
        }

        try
        {
            var counter = await _db.DocumentNumberCounters
                .FirstOrDefaultAsync(c => c.DocumentType == documentType && c.Year == year);

            if (counter is null)
            {
                counter = new DocumentNumberCounter
                {
                    DocumentType = documentType,
                    Year = year,
                    LastSequence = 0,
                    Prefix = DefaultPrefixes.GetValueOrDefault(documentType, documentType.ToUpperInvariant()),
                    PaddingWidth = 6
                };
                _db.DocumentNumberCounters.Add(counter);
            }

            counter.LastSequence++;
            await _db.SaveChangesAsync();

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync();
            }

            return $"{counter.Prefix}-{year}-{counter.LastSequence.ToString().PadLeft(counter.PaddingWidth, '0')}";
        }
        catch
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.DisposeAsync();
            }
        }
    }
}
