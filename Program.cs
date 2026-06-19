using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ChronoTrial.Data;
using ChronoTrial.Models;
using ChronoTrial.Services;
var builder = WebApplication.CreateBuilder(args);

// NIEUW
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.Cookie.Name = "ChronoTrial.Auth";
    });
builder.Services.AddAuthorization();

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

app.MapPost("/auth/login", async (HttpContext http, ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
    if (user == null)
        return Results.Redirect(BuildLoginUrl("Gebruikersnaam of wachtwoord onjuist.", returnUrl));

    var verification = hasher.VerifyHashedPassword(user, user.Wachtwoord, password);
    if (verification == PasswordVerificationResult.Failed)
        return Results.Redirect(BuildLoginUrl("Gebruikersnaam of wachtwoord onjuist.", returnUrl));

    await SignInAsync(http, user);
    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
}).DisableAntiforgery();

app.MapPost("/auth/register", async (HttpContext http, ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var passwordConfirm = form["passwordConfirm"].ToString();
    var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

    if (string.IsNullOrWhiteSpace(username))
        return Results.Redirect(BuildRegisterUrl("Vul een gebruikersnaam in.", returnUrl));

    if (string.IsNullOrWhiteSpace(email))
        return Results.Redirect(BuildRegisterUrl("Vul een e-mailadres in.", returnUrl));

    if (password != passwordConfirm)
        return Results.Redirect(BuildRegisterUrl("Wachtwoorden komen niet overeen.", returnUrl));

    if (password.Length < 6)
        return Results.Redirect(BuildRegisterUrl("Wachtwoord moet minimaal 6 tekens zijn.", returnUrl));

    var usernameExists = await db.Users.AnyAsync(u => u.Username == username);
    if (usernameExists)
        return Results.Redirect(BuildRegisterUrl("Gebruikersnaam is al in gebruik.", returnUrl));

    var emailExists = await db.Users.AnyAsync(u => u.Email == email);
    if (emailExists)
        return Results.Redirect(BuildRegisterUrl("E-mailadres is al in gebruik.", returnUrl));

    var user = new ApplicationUser
    {
        Username = username,
        Email = email
    };
    user.Wachtwoord = hasher.HashPassword(user, password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    await SignInAsync(http, user);
    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/kopen" : returnUrl);
}).DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

// Leaderboard API endpoint - wordt aangeroepen vanuit Unity
app.MapPost("/api/score", async (ScoreSubmission submission, LeaderboardService lb, ApplicationDbContext db) =>
{
    // Controleer of de user bestaat en betaald heeft
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == submission.Username);
    var hasPaid = user != null && await db.Purchases.AnyAsync(p => p.UserId == user.Id.ToString() && p.Status == "paid");
    if (user == null || !hasPaid)
        return Results.Unauthorized();

    await lb.AddAsync(new LeaderboardEntry
    {
        Username = submission.Username,
        UserId = user.Id.ToString(),
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

static string NormalizeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
        return "/";

    return returnUrl.StartsWith('/') ? returnUrl : "/";
}

static async Task SignInAsync(HttpContext http, ApplicationUser user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Email, user.Email)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
}

static string BuildLoginUrl(string error, string returnUrl)
    => QueryHelpers.AddQueryString(QueryHelpers.AddQueryString("/login", "error", error), "returnUrl", returnUrl);

static string BuildRegisterUrl(string error, string returnUrl)
    => QueryHelpers.AddQueryString(QueryHelpers.AddQueryString("/registreren", "error", error), "returnUrl", returnUrl);

public record ScoreSubmission(string Username, string Time);
