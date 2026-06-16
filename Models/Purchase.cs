namespace ChronoTrial.Models;

public class Purchase
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public decimal Amount { get; set; } = 4.99m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
