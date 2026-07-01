using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using ChronoTrial.Data;
using ChronoTrial.Models;
using ChronoTrial.Services;
var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = ResolvePostgresConnectionString(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dbConnectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(dbConnectionString));
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
builder.Services.AddScoped<EmailService>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Build")),
    RequestPath = "/Build",
    ServeUnknownFileTypes = true,
    OnPrepareResponse = context =>
    {
        var fileName = context.File.Name;
        var response = context.Context.Response;

        if (fileName.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers.ContentEncoding = "br";

            if (fileName.EndsWith(".js.br", StringComparison.OrdinalIgnoreCase))
            {
                response.ContentType = "application/javascript";
            }
            else if (fileName.EndsWith(".wasm.br", StringComparison.OrdinalIgnoreCase))
            {
                response.ContentType = "application/wasm";
            }
            else if (fileName.EndsWith(".data.br", StringComparison.OrdinalIgnoreCase))
            {
                response.ContentType = "application/octet-stream";
            }
        }
    }
});
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
        Email = email,
        Purchased = false
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

app.MapPost("/auth/forgot-password", async (HttpContext http, ApplicationDbContext db, EmailService emailSvc) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();

    if (!string.IsNullOrWhiteSpace(email))
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            // Eventuele oude, nog niet gebruikte reset-links van deze gebruiker ongeldig maken
            var oldTokens = await db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.Used)
                .ToListAsync();
            foreach (var oldToken in oldTokens)
                oldToken.Used = true;

            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync();

            var publicBaseUrl = ResolvePublicBaseUrl(http, builder.Configuration);
            var resetLink = $"{publicBaseUrl}/wachtwoord-resetten?token={Uri.EscapeDataString(token)}";
            await emailSvc.SendPasswordResetEmailAsync(user.Email, resetLink);
        }
    }


    return Results.Redirect("/wachtwoord-vergeten?sent=1");
}).DisableAntiforgery();

app.MapPost("/auth/reset-password", async (HttpContext http, ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher) =>
{
    var form = await http.Request.ReadFormAsync();
    var token = form["token"].ToString();
    var password = form["password"].ToString();
    var passwordConfirm = form["passwordConfirm"].ToString();

    if (string.IsNullOrWhiteSpace(token))
        return Results.Redirect("/wachtwoord-vergeten");

    if (password != passwordConfirm)
        return Results.Redirect(BuildResetPasswordUrl("Wachtwoorden komen niet overeen.", token));

    if (password.Length < 6)
        return Results.Redirect(BuildResetPasswordUrl("Wachtwoord moet minimaal 6 tekens zijn.", token));

    var resetToken = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Token == token);
    if (resetToken == null || resetToken.Used || resetToken.ExpiresAt < DateTime.UtcNow)
        return Results.Redirect(BuildForgotPasswordUrl("Deze link is ongeldig of verlopen. Vraag een nieuwe aan."));

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == resetToken.UserId);
    if (user == null)
        return Results.Redirect(BuildForgotPasswordUrl("Deze link is ongeldig. Vraag een nieuwe aan."));

    user.Wachtwoord = hasher.HashPassword(user, password);
    resetToken.Used = true;
    await db.SaveChangesAsync();

    return Results.Redirect("/login?reset=1");
}).DisableAntiforgery();

app.MapPost("/auth/update-account", async (HttpContext http, ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/login");

    var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdClaim, out var userId))
        return Results.Redirect("/login");

    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    var currentPassword = form["currentPassword"].ToString();
    var newPassword = form["newPassword"].ToString();
    var newPasswordConfirm = form["newPasswordConfirm"].ToString();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
        return Results.Redirect("/login");

    if (string.IsNullOrWhiteSpace(username))
        return Results.Redirect(BuildAccountUrl("Vul een gebruikersnaam in."));

    if (string.IsNullOrWhiteSpace(email))
        return Results.Redirect(BuildAccountUrl("Vul een e-mailadres in."));

    if (string.IsNullOrWhiteSpace(currentPassword))
        return Results.Redirect(BuildAccountUrl("Vul je huidige wachtwoord in om wijzigingen te bevestigen."));

    var verification = hasher.VerifyHashedPassword(user, user.Wachtwoord, currentPassword);
    if (verification == PasswordVerificationResult.Failed)
        return Results.Redirect(BuildAccountUrl("Huidig wachtwoord is onjuist."));

    var usernameTaken = await db.Users.AnyAsync(u => u.Username == username && u.Id != userId);
    if (usernameTaken)
        return Results.Redirect(BuildAccountUrl("Gebruikersnaam is al in gebruik."));

    var emailTaken = await db.Users.AnyAsync(u => u.Email == email && u.Id != userId);
    if (emailTaken)
        return Results.Redirect(BuildAccountUrl("E-mailadres is al in gebruik."));

    if (!string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(newPasswordConfirm))
    {
        if (newPassword != newPasswordConfirm)
            return Results.Redirect(BuildAccountUrl("Nieuwe wachtwoorden komen niet overeen."));

        if (newPassword.Length < 6)
            return Results.Redirect(BuildAccountUrl("Nieuw wachtwoord moet minimaal 6 tekens zijn."));

        user.Wachtwoord = hasher.HashPassword(user, newPassword);
    }

    user.Username = username;
    user.Email = email;
    await db.SaveChangesAsync();

    // Claims (o.a. naam) bevatten de oude gegevens, dus opnieuw inloggen zodat alles klopt
    await SignInAsync(http, user);

    return Results.Redirect("/account?success=1");
}).DisableAntiforgery();

app.MapPost("/auth/delete-account", async (HttpContext http, ApplicationDbContext db) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/login");

    var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (int.TryParse(userIdClaim, out var userId))
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            var userIdString = user.Id.ToString();

            var relatedLeaderboardEntries = await db.LeaderboardEntries
                .Where(l => l.UserId == userIdString)
                .ToListAsync();
            db.LeaderboardEntries.RemoveRange(relatedLeaderboardEntries);

            var relatedPurchases = await db.Purchases
                .Where(p => p.UserId == userIdString)
                .ToListAsync();
            db.Purchases.RemoveRange(relatedPurchases);

            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }
    }

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/?accountDeleted=1");
}).DisableAntiforgery();

// Leaderboard API endpoint - wordt aangeroepen vanuit Unity
app.MapPost("/api/score", async (ScoreSubmission submission, LeaderboardService lb, ApplicationDbContext db) =>
{
    // Controleer of de user bestaat en betaald heeft
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == submission.Username);
    var hasPaid = user != null && user.Purchased;
    if (user == null || !hasPaid)
        return Results.Unauthorized();

    await lb.SetTimeByUsernameAsync(submission.Username, submission.Time);
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

static string ResolvePostgresConnectionString(IConfiguration configuration)
{
    var raw = configuration.GetConnectionString("DefaultConnection")
        ?? configuration["DATABASE_URL"]
        ?? configuration["POSTGRES_URL"];

    if (string.IsNullOrWhiteSpace(raw))
        throw new InvalidOperationException("Geen database-connectionstring gevonden. Configureer ConnectionStrings:DefaultConnection of DATABASE_URL.");

    raw = raw.Trim().Trim('"', '\'');

    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return raw;
    }

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.Trim('/');

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = database,
        Username = username,
        Password = password,
        SslMode = SslMode.Require
    };

    var query = QueryHelpers.ParseQuery(uri.Query);
    if (query.TryGetValue("sslmode", out var sslMode) &&
        Enum.TryParse<SslMode>(sslMode.ToString(), true, out var parsedSslMode))
    {
        builder.SslMode = parsedSslMode;
    }

    return builder.ConnectionString;
}

static string ResolvePublicBaseUrl(HttpContext http, IConfiguration configuration)
{
    var configuredBaseUrl = configuration["App:PublicBaseUrl"]
        ?? configuration["PUBLIC_BASE_URL"];

    if (!string.IsNullOrWhiteSpace(configuredBaseUrl) &&
        Uri.TryCreate(configuredBaseUrl.Trim().Trim('"', '\''), UriKind.Absolute, out var configuredUri))
    {
        return configuredUri.GetLeftPart(UriPartial.Authority);
    }

    var scheme = http.Request.Scheme;
    var host = http.Request.Host.Value;

    if (http.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
    {
        var value = forwardedProto.ToString().Split(',')[0].Trim();
        if (!string.IsNullOrWhiteSpace(value))
            scheme = value;
    }

    if (http.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
    {
        var value = forwardedHost.ToString().Split(',')[0].Trim();
        if (!string.IsNullOrWhiteSpace(value))
            host = value;
    }

    if (string.IsNullOrWhiteSpace(host))
        throw new InvalidOperationException("Kan publieke host niet bepalen voor reset-link.");

    return $"{scheme}://{host}";
}

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

static string BuildAccountUrl(string error)
    => QueryHelpers.AddQueryString("/account", "error", error);

static string BuildForgotPasswordUrl(string error)
    => QueryHelpers.AddQueryString("/wachtwoord-vergeten", "error", error);

static string BuildResetPasswordUrl(string error, string token)
    => QueryHelpers.AddQueryString(QueryHelpers.AddQueryString("/wachtwoord-resetten", "error", error), "token", token);

public record ScoreSubmission(string Username, string Time);
