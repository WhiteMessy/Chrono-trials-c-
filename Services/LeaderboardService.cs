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
        var entries = await _db.LeaderboardEntries.ToListAsync();
        return entries
            .OrderBy(e => e.TimeInSeconds)
            .Take(count)
            .ToList();
    }

    public async Task AddAsync(LeaderboardEntry entry)
    {
        _db.LeaderboardEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entry = await _db.LeaderboardEntries.FindAsync(id);
        if (entry != null)
        {
            _db.LeaderboardEntries.Remove(entry);
            await _db.SaveChangesAsync();
        }
    }
}
