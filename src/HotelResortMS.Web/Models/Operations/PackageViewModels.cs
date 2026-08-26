using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Operations;

public class PackageComponentInput
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class PackageEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public int Capacity { get; set; }

    [Required, DataType(DataType.Date)]
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    public DateOnly? ExpirationDate { get; set; }

    public bool IsActive { get; set; } = true;

    public List<PackageComponentInput> Components { get; set; } = new();
}
