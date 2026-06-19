using Microsoft.EntityFrameworkCore;
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

    public PaymentService(ApplicationDbContext db)
    {
        _db = db;
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
        var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.OrderId == orderId && p.UserId == userId);
        if (purchase == null) return false;

        purchase.Status = "paid";

        await _db.SaveChangesAsync();
        return true;
    }
}
