using System.ComponentModel.DataAnnotations.Schema;

namespace ChronoTrial.Models;

[Table("leaderboard")]
public class LeaderboardEntry
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public double TimeInSeconds
    {
        get
        {
            var parts = Time.Split(':', '.');
            if (parts.Length == 3)
                return int.Parse(parts[0]) * 60 + int.Parse(parts[1]) + int.Parse(parts[2]) / 1000.0;
            if (parts.Length == 2)
                return int.Parse(parts[0]) * 60 + double.Parse(parts[1]);
            return double.MaxValue;
        }
    }
}