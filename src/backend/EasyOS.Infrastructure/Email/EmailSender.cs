using EasyOS.Domain.Platform;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EasyOS.Infrastructure.Email;

public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);

public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>SMTP padrão da plataforma, usado quando o tenant não tem SMTP próprio.</summary>
public sealed class DefaultSmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; } = "EasyOS";
}

/// <summary>Envia e-mail com o SMTP configurado para o tenant atual (ou o padrão da plataforma).</summary>
public sealed class TenantEmailSender(
    ITenantContext tenantContext,
    ISecretProtector protector,
    IOptions<DefaultSmtpOptions> defaults,
    ILogger<TenantEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var (host, port, ssl, user, password, from, fromName) = ResolveSmtp(tenantContext.Tenant.Settings.Smtp);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromName ?? tenantContext.Tenant.Name, from));
        foreach (var to in message.To.Where(t => !string.IsNullOrWhiteSpace(t)))
            mime.To.Add(MailboxAddress.Parse(to.Trim()));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var a in message.Attachments ?? [])
            body.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        var socket = ssl ? (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls) : SecureSocketOptions.Auto;
        await client.ConnectAsync(host, port, socket, ct);
        if (!string.IsNullOrEmpty(user)) await client.AuthenticateAsync(user, password ?? "", ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("E-mail '{Subject}' enviado para {Count} destinatário(s)", message.Subject, mime.To.Count);
    }

    private (string Host, int Port, bool Ssl, string? User, string? Password, string From, string? FromName) ResolveSmtp(SmtpSettings smtp)
    {
        if (smtp.IsConfigured)
        {
            var password = smtp.PasswordProtected is null ? null : protector.Unprotect(smtp.PasswordProtected);
            return (smtp.Host!, smtp.Port, smtp.EnableSsl, smtp.UserName ?? smtp.FromAddress, password, smtp.FromAddress!, smtp.FromName);
        }

        var d = defaults.Value;
        if (string.IsNullOrWhiteSpace(d.Host) || string.IsNullOrWhiteSpace(d.FromAddress))
            throw new InvalidOperationException("Nenhum servidor SMTP configurado para este cliente.");
        return (d.Host, d.Port, d.EnableSsl, d.UserName, d.Password, d.FromAddress, d.FromName);
    }
}
