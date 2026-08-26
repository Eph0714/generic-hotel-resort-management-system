using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Operations;

public enum EventLookupType
{
    EventType,
    EventVenue
}

public class EventLookupItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
}

public class EventLookupEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public int Capacity { get; set; }

    public EventLookupType Type { get; set; }
}

public class EventCreateViewModel
{
    [Required, Display(Name = "Event Type")]
    public int EventTypeId { get; set; }

    [Required, Display(Name = "Venue")]
    public int EventVenueId { get; set; }

    public int? GuestId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientContact { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    public int ExpectedGuests { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DepositAmount { get; set; }

    public string? Notes { get; set; }

    public List<EventType> EventTypes { get; set; } = new();
    public List<EventVenue> Venues { get; set; } = new();
    public List<Guest> Guests { get; set; } = new();
}
