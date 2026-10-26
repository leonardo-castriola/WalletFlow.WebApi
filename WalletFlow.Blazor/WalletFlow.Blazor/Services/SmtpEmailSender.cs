using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WalletFlow.WebApi.Services;

namespace WalletFlow.Blazor.Services;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Smtp:Host"];
        var port = configuration.GetValue<int?>("Email:Smtp:Port");
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var senderAddress = configuration["Email:Smtp:From"];
        var implicitTls = configuration.GetValue<bool>("Email:Smtp:ImplicitTls");

        if (string.IsNullOrWhiteSpace(host) || port is null or < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(senderAddress))
            throw new InvalidOperationException("A configuração SMTP está incompleta.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(senderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = implicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(host, port.Value, socketOptions, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
