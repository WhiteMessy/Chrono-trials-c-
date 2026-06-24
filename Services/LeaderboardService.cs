using Microsoft.EntityFrameworkCore;
using ChronoTrial.Data;
using ChronoTrial.Models;

namespace ChronoTrial.Services;

public class LeaderboardService
{
    private readonly ApplicationDbContext _db;

    public LeaderboardService(ApplicationDbContext db) => _db = db;

    public async Task<List<LeaderboardEntry>> GetTopAsync(int count = 50)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(user => user.Time != null)
            .OrderBy(user => user.Time)
            .Take(count)
            .Select(user => new LeaderboardEntry
            {
                Id = user.Id,
                Username = user.Username,
                UserId = user.Id.ToString(),
                Time = FormatTime(user.Time!.Value),
                Date = user.TimeSetAt ?? DateTime.UtcNow
            })
            .ToListAsync();

        return users;
    }

    public async Task SetTimeAsync(int userId, string time)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return;

        if (!TryParseTime(time, out var newTime))
            return;

        if (user.Time.HasValue && user.Time.Value <= newTime)
            return;

        user.Time = newTime;
        user.TimeSetAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task SetTimeByUsernameAsync(string username, string time)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return;

        if (!TryParseTime(time, out var newTime))
            return;

        if (user.Time.HasValue && user.Time.Value <= newTime)
            return;

        user.Time = newTime;
        user.TimeSetAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.Time = null;
            user.TimeSetAt = null;
            await _db.SaveChangesAsync();
        }
    }

    private static bool TryParseTime(string time, out double seconds)
    {
        seconds = double.MaxValue;

        if (string.IsNullOrWhiteSpace(time))
            return false;

        var parts = time.Trim().Split(':', '.');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var minutes) &&
            int.TryParse(parts[1], out var wholeSeconds) &&
            int.TryParse(parts[2], out var milliseconds))
        {
            seconds = minutes * 60 + wholeSeconds + milliseconds / 1000.0;
            return true;
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var onlyMinutes) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fractionalSeconds))
        {
            seconds = onlyMinutes * 60 + fractionalSeconds;
            return true;
        }

        if (double.TryParse(time, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var directSeconds))
        {
            seconds = directSeconds;
            return true;
        }

        return false;
    }

    private static string FormatTime(double seconds)
    {
        var wholeMinutes = (int)(seconds / 60);
        var remainingSeconds = seconds - wholeMinutes * 60;
        return $"{wholeMinutes:00}:{remainingSeconds:00.000}";
    }
}
