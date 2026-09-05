using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Queues an event for every endpoint that asked for it.
///
/// Queued rather than posted inline, deliberately. A webhook posted during the
/// request means the customer's slow endpoint becomes the CRM's slow endpoint,
/// and a receiver that is down turns "create a lead" into an error for the
/// person creating it. What happens in the request is one insert.
/// </summary>
public class WebhookDispatcher(AppDbContext db, ILogger<WebhookDispatcher> logger)
{
    /// <summary>
    /// The same shape the rest of the API sends: camel-cased names and UTC
    /// timestamps with a Z.
    ///
    /// Not the serialiser's defaults, which would put PascalCase property names
    /// in the payload — an integration reading <c>data.name</c> from the REST
    /// API and <c>data.Name</c> from the webhook for the same record is a bug
    /// report waiting to be filed.
    /// </summary>
    private static readonly JsonSerializerOptions PayloadFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new UtcDateTimeConverter(), new NullableUtcDateTimeConverter() },
    };

    /// <summary>
    /// Records the event for delivery. Never throws: a failure to queue a
    /// notification must not fail the business action that caused it.
    /// </summary>
    public async Task RaiseAsync(string eventName, object payload, CancellationToken ct = default)
    {
        try
        {
            var endpoints = await db.WebhookEndpoints
                .Where(e => e.IsActive)
                .ToListAsync(ct);

            var wanted = endpoints.Where(e => e.Wants(eventName)).ToList();
            if (wanted.Count == 0) return;

            var body = JsonSerializer.Serialize(new
            {
                @event = eventName,
                occurredAt = DateTime.UtcNow,
                data = payload,
            }, PayloadFormat);

            foreach (var endpoint in wanted)
            {
                db.WebhookDeliveries.Add(new WebhookDelivery
                {
                    CompanyId = endpoint.CompanyId,
                    EndpointId = endpoint.Id,
                    Event = eventName,
                    Payload = body,
                });
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Could not queue the {Event} webhook.", eventName);
        }
    }
}

/// <summary>
/// Posts the queued deliveries, with a backoff and a signature.
///
/// Runs on its own schedule rather than reacting to the queue, because the
/// retry ladder needs a clock either way and one loop is easier to reason about
/// than a loop plus a trigger.
/// </summary>
public class WebhookSender(
    AppDbContext db, IHttpClientFactory http, ILogger<WebhookSender> logger)
{
    /// <summary>
    /// Attempts before a delivery is given up on, and the wait before each.
    ///
    /// The ladder is short and front-loaded: most failures are a receiver
    /// restarting, and the ones that are not will not be fixed by waiting a day.
    /// A delivery that runs out sits in the log marked Failed, which is what the
    /// replay button reads.
    /// </summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    /// <summary>Consecutive failures before an endpoint is switched off.</summary>
    private const int FailuresBeforeDisable = 20;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var due = await db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Include(d => d.Endpoint)
            .Where(d => d.Status == DeliveryStatus.Pending
                        && d.NextAttemptAt != null
                        && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var delivery in due)
        {
            if (delivery.Endpoint is null || !delivery.Endpoint.IsActive)
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.Error = "The endpoint was removed or switched off.";
                continue;
            }

            await AttemptAsync(delivery, ct);
        }

        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task AttemptAsync(WebhookDelivery delivery, CancellationToken ct)
    {
        var endpoint = delivery.Endpoint!;

        delivery.Attempts++;
        delivery.LastAttemptAt = DateTime.UtcNow;

        try
        {
            var client = http.CreateClient("webhooks");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
            };

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            request.Headers.Add("X-BullEvents-Event", delivery.Event);
            request.Headers.Add("X-BullEvents-Delivery", delivery.Id.ToString());
            request.Headers.Add("X-BullEvents-Timestamp", timestamp);

            // Signed over the timestamp and the body together. Signing the body
            // alone lets somebody replay a captured delivery forever; including
            // the timestamp lets the receiver reject anything stale.
            request.Headers.Add("X-BullEvents-Signature", Sign(endpoint.Secret, timestamp, delivery.Payload));

            using var response = await client.SendAsync(request, ct);

            delivery.ResponseCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = DeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.NextAttemptAt = null;
                delivery.Error = null;

                endpoint.ConsecutiveFailures = 0;
                return;
            }

            Retry(delivery, endpoint, $"Responded {(int)response.StatusCode}.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Retry(delivery, endpoint, Trim(error.Message));
        }
    }

    private void Retry(WebhookDelivery delivery, WebhookEndpoint endpoint, string reason)
    {
        delivery.Error = reason;
        endpoint.ConsecutiveFailures++;

        if (delivery.Attempts >= Backoff.Length)
        {
            delivery.Status = DeliveryStatus.Failed;
            delivery.NextAttemptAt = null;
        }
        else
        {
            delivery.NextAttemptAt = DateTime.UtcNow.Add(Backoff[delivery.Attempts]);
        }

        if (endpoint.ConsecutiveFailures >= FailuresBeforeDisable && endpoint.IsActive)
        {
            endpoint.IsActive = false;
            endpoint.DisabledAt = DateTime.UtcNow;
            endpoint.DisabledReason =
                $"Switched off after {FailuresBeforeDisable} failures in a row. Last: {reason}";

            logger.LogWarning(
                "Webhook endpoint {Endpoint} switched off after repeated failures.", endpoint.Url);
        }
    }

    /// <summary>
    /// The signature a receiver checks: HMAC-SHA256 over
    /// <c>{timestamp}.{body}</c>, hex-encoded.
    /// </summary>
    public static string Sign(string secret, string timestamp, string body)
    {
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{body}"));

        return $"sha256={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    private static string Trim(string message) =>
        message.Length <= 300 ? message : message[..300];
}

/// <summary>Drives the sender. Short interval, because a webhook late is a webhook wrong.</summary>
public class WebhookHost(IServiceProvider services, ILogger<WebhookHost> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<WebhookSender>();
                await sender.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Webhook delivery pass failed; will retry.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
