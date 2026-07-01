using System.Net;
using System.Net.Mail;

namespace ChronoTrial.Services;

// Simpele service om e-mails te versturen via SMTP.
// De SMTP-instellingen komen uit appsettings.json (of environment variables / user-secrets).
// Is er geen SMTP-host geconfigureerd (bv. lokaal ontwikkelen zonder mailserver),
// dan wordt de e-mail niet verstuurd maar wel gelogd, zodat de reset-link nog te vinden is.
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var host = _config["Smtp:Host"];

        const string subject = "Wachtwoord opnieuw instellen — Chrono Trials";
        var body =
            $"""
             Hallo,

             Je hebt een verzoek gedaan om het wachtwoord van je Chrono Trials account opnieuw in te stellen.

             Klik op onderstaande link om een nieuw wachtwoord in te stellen:
             {resetLink}

             Deze link is 1 uur geldig. Heb je dit verzoek niet zelf gedaan? Dan kun je deze e-mail negeren,
             je wachtwoord blijft dan ongewijzigd.

             — Chrono Trials
             """;

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning(
                "SMTP is niet geconfigureerd (Smtp:Host ontbreekt in appsettings). " +
                "Wachtwoord-reset e-mail voor {Email} is NIET verstuurd. Reset-link: {Link}",
                toEmail, resetLink);
            return;
        }

        var port = int.TryParse(_config["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out var parsedSsl) || parsedSsl;
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];
        var from = _config["Smtp:From"];
        if (string.IsNullOrWhiteSpace(from))
            from = username;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(from!, toEmail, subject, body);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Versturen van wachtwoord-reset e-mail naar {Email} is mislukt.", toEmail);
            throw;
        }
    }
}