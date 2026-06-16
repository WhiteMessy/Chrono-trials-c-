using Microsoft.AspNetCore.Identity;
using ChronoTrial.Data;
using ChronoTrial.Models;

namespace ChronoTrial.Services;

/// <summary>
/// Gesimuleerde betaalservice - werkt zonder echte Mollie account.
/// Voor productie: vervang door echte Mollie integratie.
/// </summary>
public class PaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PaymentService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Maak een nep-betaling aan en geef een order ID terug
    public async Task<string> CreateOrderAsync(string userId)
    {
        var orderId = $"CT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        _db.Purchases.Add(new Purchase
        {
            UserId = userId,
            OrderId = orderId,
            Status = "pending"
        });
        await _db.SaveChangesAsync();

        return orderId;
    }

    // Simuleer een geslaagde betaling (demo modus)
    public async Task<bool> CompletePaymentAsync(string orderId, string userId)
    {
        var purchase = _db.Purchases.FirstOrDefault(p => p.OrderId == orderId && p.UserId == userId);
        if (purchase == null) return false;

        purchase.Status = "paid";
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.HasPurchased = true;
            user.PurchaseDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync();
        return true;
    }
}
