using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ChronoTrial.Data;
using ChronoTrial.Models;
using ChronoTrial.Services;
using Npgsql.EntityFrameworkCore.PostgreSQL;
var builder = WebApplication.CreateBuilder(args);

// NIEUW
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));
// Identity - inlogsysteem
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Onze eigen services
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Leaderboard API endpoint - wordt aangeroepen vanuit Unity
app.MapPost("/api/score", async (ScoreSubmission submission, LeaderboardService lb, 
    UserManager<ApplicationUser> userManager) =>
{
    // Controleer of de user bestaat en betaald heeft
    var user = await userManager.FindByNameAsync(submission.Username);
    if (user == null || !user.HasPurchased)
        return Results.Unauthorized();

    await lb.AddAsync(new LeaderboardEntry
    {
        Username = submission.Username,
        UserId = user.Id,
        Time = submission.Time,
        Date = DateTime.UtcNow
    });
    return Results.Ok();
});

app.MapGet("/api/leaderboard", async (LeaderboardService lb) =>
{
    var entries = await lb.GetTopAsync(50);
    return Results.Ok(entries);
});

app.MapRazorPages();

app.MapRazorComponents<ChronoTrial.Pages.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public record ScoreSubmission(string Username, string Time);
