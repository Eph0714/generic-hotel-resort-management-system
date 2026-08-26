using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Guests;

public class GuestEditViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    [Display(Name = "Guest Type")]
    public string? GuestType { get; set; }

    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Display(Name = "Senior Citizen")]
    public bool IsSeniorCitizen { get; set; }
    [Display(Name = "Senior Citizen ID")]
    public string? SeniorCitizenIdNumber { get; set; }

    [Display(Name = "PWD")]
    public bool IsPwd { get; set; }
    [Display(Name = "PWD ID")]
    public string? PwdIdNumber { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
