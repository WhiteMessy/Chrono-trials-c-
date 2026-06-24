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
        await Task.CompletedTask;
        return $"CT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    // Simuleer een geslaagde betaling (demo modus)
    public async Task<bool> CompletePaymentAsync(string orderId, string userId)
    {
        if (!int.TryParse(userId, out var parsedUserId))
            return false;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == parsedUserId);
        if (user == null)
            return false;

        user.Purchased = true;
        await _db.SaveChangesAsync();
        return true;
    }
}
