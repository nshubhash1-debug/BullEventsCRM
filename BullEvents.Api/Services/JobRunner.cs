using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Runs the recurring jobs an administrator has scheduled, and records what
/// happened.
///
/// The outcome is written back onto the job row rather than only to a log,
/// because the question somebody opens the Scheduled jobs screen with is "did
/// it run, and did it work" — and an answer that requires reading a log file on
/// a server is not an answer.
///
/// Jobs are looked up by kind against a fixed registry rather than being
/// arbitrary code. A scheduler that can run anything is a remote execution
/// endpoint with a friendly name on it.
/// </summary>
public class JobRunner(IServiceProvider services, ILogger<JobRunner> logger)
{
    /// <summary>Runs everything that is due. Returns how many ran.</summary>
    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var due = await db.ScheduledJobs
            .IgnoreQueryFilters()
            .Where(j => j.IsActive && (j.NextRunAt == null || j.NextRunAt <= now))
            .ToListAsync(ct);

        var ran = 0;

        foreach (var job in due)
        {
            await RunAsync(job, db, scope.ServiceProvider, ct);
            ran++;
        }

        if (ran > 0) await db.SaveChangesAsync(ct);

        return ran;
    }

    /// <summary>Runs one job now, whatever its schedule says.</summary>
    public async Task<ScheduledJob> RunNowAsync(int jobId, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScheduledJobs
            .IgnoreQueryFilters()
            .FirstAsync(j => j.Id == jobId, ct);

        await RunAsync(job, db, scope.ServiceProvider, ct);
        await db.SaveChangesAsync(ct);

        return job;
    }

    private async Task RunAsync(
        ScheduledJob job, AppDbContext db, IServiceProvider provider, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var message = job.Kind switch
            {
                JobKinds.EscalationSweep => await SweepAsync(provider, ct),
                JobKinds.InterestAccrual => await AccrueAsync(provider, ct),
                JobKinds.UsageSnapshot => await UsageAsync(provider, ct),

                // Named in the catalogue but not wired to anything yet. Reported
                // as skipped rather than as a success, so the screen never claims
                // work that did not happen.
                _ => $"'{job.Kind}' has no runner yet — nothing was done.",
            };

            job.LastOutcome = job.Kind is JobKinds.EscalationSweep
                or JobKinds.InterestAccrual or JobKinds.UsageSnapshot
                ? "Ok"
                : "Skipped";

            job.LastMessage = message;
        }
        catch (Exception error)
        {
            // A failing job must not take the host down, and must not look like
            // it succeeded. Both halves matter.
            logger.LogError(error, "Scheduled job {Job} failed.", job.Name);

            job.LastOutcome = "Failed";
            job.LastMessage = error.Message;
            job.FailureCount++;
        }

        clock.Stop();

        job.LastRunAt = started;
        job.LastDurationMs = (int)clock.ElapsedMilliseconds;
        job.RunCount++;
        job.NextRunAt = Cron.Next(job.Cron, started);
        job.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task<string> SweepAsync(IServiceProvider provider, CancellationToken ct)
    {
        var sweep = provider.GetRequiredService<EscalationSweep>();
        var result = await sweep.RunAsync(ct);

        return $"{result.Checked} checked, {result.Breached} past target, {result.Acted} acted on.";
    }

    private static async Task<string> AccrueAsync(IServiceProvider provider, CancellationToken ct)
    {
        var accrual = provider.GetRequiredService<InterestAccrual>();
        var count = await accrual.RunAsync(ct);

        return $"Penal interest brought up to date on {count} overdue bookings.";
    }

    private static async Task<string> UsageAsync(IServiceProvider provider, CancellationToken ct)
    {
        var recorder = provider.GetRequiredService<UsageRecorder>();
        await recorder.RunAsync(ct);

        return "Usage recorded for every tenant.";
    }
}

/// <summary>
/// Just enough cron to answer "when does this run next".
///
/// Standard five fields — minute, hour, day of month, month, day of week — with
/// <c>*</c>, lists, ranges and <c>*/n</c> steps. Deliberately not a full
/// implementation: the exotic corners of cron are where the bugs live, and an
/// administrator scheduling a nightly sweep does not need <c>L</c> or <c>#</c>.
/// A field that cannot be parsed is treated as "any", which errs towards
/// running too often rather than silently never running at all.
/// </summary>
public static class Cron
{
    /// <summary>The next instant at or after <paramref name="after"/> that matches.</summary>
    public static DateTime? Next(string expression, DateTime after)
    {
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) return after.AddHours(1);

        var minutes = Parse(fields[0], 0, 59);
        var hours = Parse(fields[1], 0, 23);
        var days = Parse(fields[2], 1, 31);
        var months = Parse(fields[3], 1, 12);
        var weekdays = Parse(fields[4], 0, 6);

        // Minute resolution, walked forward. Four years covers the worst honest
        // case — 29 February — and stops a nonsensical expression spinning.
        var cursor = after.AddMinutes(1);
        cursor = cursor.AddSeconds(-cursor.Second).AddMilliseconds(-cursor.Millisecond);

        var limit = after.AddYears(4);

        while (cursor < limit)
        {
            if (months.Contains(cursor.Month)
                && days.Contains(cursor.Day)
                && weekdays.Contains((int)cursor.DayOfWeek)
                && hours.Contains(cursor.Hour)
                && minutes.Contains(cursor.Minute))
            {
                return cursor;
            }

            cursor = cursor.AddMinutes(1);
        }

        return null;
    }

    /// <summary>A human sentence for a cron expression, for the screen.</summary>
    public static string Describe(string expression)
    {
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) return expression;

        var (minute, hour, day, month, weekday) =
            (fields[0], fields[1], fields[2], fields[3], fields[4]);

        if (minute.StartsWith("*/") && hour == "*")
        {
            return $"Every {minute[2..]} minutes";
        }

        if (hour.StartsWith("*/") && minute == "0")
        {
            return $"Every {hour[2..]} hours";
        }

        if (minute == "*" && hour == "*") return "Every minute";

        if (int.TryParse(minute, out var m) && int.TryParse(hour, out var h))
        {
            var at = $"at {h:D2}:{m:D2}";

            if (day == "*" && month == "*" && weekday == "*") return $"Daily {at}";
            if (weekday != "*") return $"Every {Weekday(weekday)} {at}";
            if (day != "*") return $"On day {day} of the month {at}";
        }

        if (int.TryParse(minute, out var only) && hour == "*") return $"Hourly at {only:D2} past";

        return expression;
    }

    private static string Weekday(string field) => field switch
    {
        "0" => "Sunday", "1" => "Monday", "2" => "Tuesday", "3" => "Wednesday",
        "4" => "Thursday", "5" => "Friday", "6" => "Saturday",
        "1-5" => "weekday",
        _ => $"day {field}",
    };

    private static HashSet<int> Parse(string field, int min, int max)
    {
        var all = Enumerable.Range(min, max - min + 1).ToHashSet();

        if (field == "*") return all;

        var result = new HashSet<int>();

        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("*/") && int.TryParse(part[2..], out var step) && step > 0)
            {
                for (var value = min; value <= max; value += step) result.Add(value);
                continue;
            }

            if (part.Contains('-'))
            {
                var ends = part.Split('-', 2);

                if (int.TryParse(ends[0], out var from) && int.TryParse(ends[1], out var to))
                {
                    for (var value = from; value <= to && value <= max; value++) result.Add(value);
                }

                continue;
            }

            if (int.TryParse(part, out var single)) result.Add(single);
        }

        return result.Count == 0 ? all : result;
    }
}

/// <summary>Wakes every minute and runs whatever has come due.</summary>
public class JobHost(IServiceProvider services, ILogger<JobHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Long enough for migrations and seeding to finish first.
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<JobRunner>();
                await runner.RunDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Scheduled job pass failed; will retry.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
