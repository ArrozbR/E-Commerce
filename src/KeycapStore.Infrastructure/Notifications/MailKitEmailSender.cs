using KeycapStore.Application.Notifications;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class MailKitEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var mime = Build(settings, message);
        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };

        try
        {
            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (SmtpCommandException ex)
        {
            var permanent = ex.ErrorCode == SmtpErrorCode.RecipientNotAccepted && (int)ex.StatusCode >= 500;
            throw new EmailDeliveryException(
                $"O servidor de e-mail recusou o envio ({(int)ex.StatusCode}, {ex.ErrorCode}).",
                permanent,
                ex);
        }
    }

    private static MimeMessage Build(EmailOptions settings, EmailMessage message)
    {
        if (!MailboxAddress.TryParse(message.To, out var to))
        {
            throw new EmailDeliveryException("O endereço do destinatário é inválido.", permanent: true);
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(to);
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        return mime;
    }
}
