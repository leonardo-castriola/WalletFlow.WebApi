using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WalletFlow.Application.Abstractions;

namespace WalletFlow.Infrastructure.Email;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        SendAsync(recipient, subject, htmlBody, Array.Empty<EmailAttachment>(), cancellationToken);

    public async Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken = default)
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

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(senderAddress!));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody };
        foreach (var attachment in attachments)
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = implicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(host!, port!.Value, socketOptions, cancellationToken);
        await client.AuthenticateAsync(username!, password!, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        logger.LogInformation("The SMTP server accepted an outbound email.");
        await client.DisconnectAsync(true, cancellationToken);
    }
}
