using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;

namespace BullEvents.Api.Services;

/// <summary>
/// Sends transactional mail. One interface so the sign-in path does not know or
/// care whether a provider is connected.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);

    /// <summary>False when nothing is actually delivered — the UI says so rather than lying.</summary>
    bool Delivers { get; }
}

/// <summary>
/// The stand-in used until a provider is configured: the message is written to
/// the log and nowhere else.
///
/// It exists so local development needs no credentials, and it is deliberately
/// loud about not delivering — a silent no-op sender is how a team discovers in
/// production that nobody has received a code for three weeks.
/// </summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public bool Delivers => false;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogWarning(
            "No email provider is configured, so nothing was sent. "
            + "Intended recipient {To}, subject {Subject}.\n{Body}",
            to, subject, body);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Amazon SES.
///
/// Credentials are not read here: the SDK's default chain finds them from the
/// instance role, the environment or the shared profile, in that order. That is
/// what lets the deployed box carry no secret at all — the role is attached to
/// the machine and rotates itself.
/// </summary>
public class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly string _from;
    private readonly ILogger<SesEmailSender> _logger;

    public bool Delivers => true;

    public SesEmailSender(IConfiguration config, ILogger<SesEmailSender> logger)
    {
        _logger = logger;

        _from = config["Email:FromAddress"]
            ?? throw new InvalidOperationException(
                "Email:FromAddress is not configured. SES refuses mail from an unverified sender.");

        var region = config["Email:Region"] ?? config["AWS:Region"] ?? "ap-south-1";
        _ses = new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(region));
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = _from,
            Destination = new Destination { ToAddresses = [to] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body { Text = new Content { Data = body, Charset = "UTF-8" } },
                },
            },
        };

        try
        {
            await _ses.SendEmailAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Rethrown rather than swallowed: a sign-in that cannot deliver its
            // code has failed, and telling the user "check your email" when
            // nothing was sent leaves them waiting on a message that will never
            // arrive.
            _logger.LogError(ex, "SES refused the message to {To}.", to);
            throw;
        }
    }
}
