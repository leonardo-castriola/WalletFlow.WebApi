using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WalletFlow.WebApi.Services;

namespace WalletFlow.Blazor.Services;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Smtp:Host"];
        var port = configuration.GetValue<int?>("Email:Smtp:Port");
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var senderAddress = configuration["Email:Smtp:From"];
        var implicitTls = configuration.GetValue<bool>("Email:Smtp:ImplicitTls");

        var missingSettings = new List<string>();
        if (string.IsNullOrWhiteSpace(host)) missingSettings.Add("Email:Smtp:Host");
        if (port is null or < 1 or > 65535) missingSettings.Add("Email:Smtp:Port");
        if (string.IsNullOrWhiteSpace(username)) missingSettings.Add("Email:Smtp:Username");
        if (string.IsNullOrWhiteSpace(password)) missingSettings.Add("Email:Smtp:Password");
        if (string.IsNullOrWhiteSpace(senderAddress)) missingSettings.Add("Email:Smtp:From");
        if (missingSettings.Count > 0)
            throw new InvalidOperationException(
                $"Configuração SMTP incompleta. Verifique: {string.Join(", ", missingSettings)}.");

        var smtpHost = host!;
        var smtpPort = port!.Value;
        var smtpUsername = username!;
        var smtpPassword = password!;
        var smtpSenderAddress = senderAddress!;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtpSenderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = implicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(smtpHost, smtpPort, socketOptions, cancellationToken);
        await client.AuthenticateAsync(smtpUsername, smtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        logger.LogInformation("The SMTP server accepted an outbound email.");
        await client.DisconnectAsync(true, cancellationToken);
    }
}
