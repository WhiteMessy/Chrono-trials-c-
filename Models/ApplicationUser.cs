using Microsoft.AspNetCore.Identity;

namespace ChronoTrial.Models;

public class ApplicationUser : IdentityUser
{
    public bool HasPurchased { get; set; } = false;
    public DateTime? PurchaseDate { get; set; }
    public string? DisplayName { get; set; }
}
