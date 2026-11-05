namespace WalletFlow.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);

    Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken = default);
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
