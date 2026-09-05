using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Ml;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The call log. Notes are run through the local sentiment model on write, so
/// the tone is stored with the call rather than recomputed every time the list
/// is opened.
/// </summary>
[ApiController]
[Route("api/calls")]
[Authorize]
[SecuredBy(SecuredObjects.Call)]
public class CallsController(AppDbContext db, SentimentService sentiment) : CrmControllerBase(db)
{
    internal static readonly FieldMap<CallLog> Fields = new FieldMap<CallLog>()
        .Text("relatedName", "Related to", searchable: true)
        .Select("relatedType", "Related object")
        .Number("relatedId", "Related ID")
        .Select("direction", "Direction")
        .Select("outcome", "Outcome")
        .Select("disposition", "Disposition")
        .Text("phoneNumber", "Phone", searchable: true)
        .Date("startedAt", "Started")
        .Number("durationSeconds", "Duration (sec)")
        .Number("waitSeconds", "Wait (sec)")
        .Select("agentName", "Agent")
        .Number("agentId", "Agent ID")
        .Text("notes", "Notes", searchable: true)
        .Number("sentimentScore", "Sentiment score")
        .Select("sentimentLabel", "Sentiment")
        .Date("followUpAt", "Follow-up due")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("branchId", "Branch ID")
        .Date("createdAt", "Logged");

    private IQueryable<CallLog> Base() => Db.CallLogs.Include(c => c.Branch).AsNoTracking();

    private static CallLogDto ToDto(CallLog c) => new(
        c.Id, c.RelatedType, c.RelatedId, c.RelatedName,
        c.Direction, c.Outcome, c.Disposition, c.PhoneNumber,
        c.StartedAt, c.DurationSeconds, c.WaitSeconds,
        c.AgentId, c.AgentName, c.Notes, c.RecordingUrl,
        c.SentimentScore, c.SentimentLabel, c.FollowUpAt,
        c.BranchId, c.Branch?.Name ?? "—", c.CreatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var agents = await Db.CallLogs
            .Select(c => c.AgentName).Distinct().OrderBy(n => n).Take(200).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["relatedType"] = Options(RelatedTypes.All),
            ["direction"] = Options(CallDirections.All),
            ["outcome"] = Options(CallOutcomes.All),
            ["disposition"] = Options(CallDispositions.All),
            ["sentimentLabel"] = Options("Positive", "Neutral", "Negative"),
            ["agentName"] = Options([.. agents]),
            ["branchName"] = Options([.. branches]),
        }, new Dictionary<string, string>
        {
            ["direction"] = "Call",
            ["outcome"] = "Call",
            ["disposition"] = "Call",
            ["phoneNumber"] = "Call",
            ["startedAt"] = "Timing",
            ["durationSeconds"] = "Timing",
            ["waitSeconds"] = "Timing",
            ["followUpAt"] = "Timing",
            ["notes"] = "Content",
            ["sentimentScore"] = "Content",
            ["sentimentLabel"] = "Content",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<CallLogDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        return Ok(await RunQueryAsync(
            Base(),
            request,
            Fields,
            ToDto,
            defaultSortPath: "StartedAt",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["calls"] = await filtered.CountAsync(ct),
                ["connected"] = await filtered.CountAsync(c => c.Outcome == CallOutcomes.Connected, ct),
                ["talkMinutes"] = decimal.Round(
                    (decimal)await filtered.SumAsync(c => (double)c.DurationSeconds, ct) / 60m, 1),
                ["positive"] = await filtered.CountAsync(c => c.SentimentLabel == "Positive", ct),
            },
            cancellationToken: ct));
    }

    /// <summary>
    /// Per-agent call performance for the leaderboard — connect rate, talk time
    /// and how often a call actually moved something forward.
    /// </summary>
    [HttpGet("scorecard")]
    public async Task<ActionResult<IReadOnlyList<CallScorecardDto>>> Scorecard(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));

        var rows = await Db.CallLogs
            .Where(c => c.StartedAt >= since)
            .GroupBy(c => new { c.AgentId, c.AgentName })
            .Select(g => new
            {
                g.Key.AgentId,
                g.Key.AgentName,
                Total = g.Count(),
                Connected = g.Count(c => c.Outcome == CallOutcomes.Connected),
                TalkSeconds = g.Sum(c => c.DurationSeconds),
                Positive = g.Count(c => c.SentimentLabel == "Positive"),
                Progressed = g.Count(c =>
                    c.Disposition == CallDispositions.Interested ||
                    c.Disposition == CallDispositions.SiteVisitScheduled ||
                    c.Disposition == CallDispositions.Converted),
            })
            .ToListAsync(ct);

        return Ok(rows
            .Select(r => new CallScorecardDto(
                r.AgentId,
                r.AgentName,
                r.Total,
                r.Connected,
                r.Total == 0 ? 0 : Math.Round(r.Connected * 100.0 / r.Total, 1),
                Math.Round(r.TalkSeconds / 60.0, 1),
                r.Connected == 0 ? 0 : Math.Round(r.TalkSeconds / (double)r.Connected, 0),
                r.Progressed,
                r.Total == 0 ? 0 : Math.Round(r.Progressed * 100.0 / r.Total, 1),
                r.Positive))
            .OrderByDescending(r => r.Calls)
            .ToList());
    }

    /// <summary>
    /// Everything a telephony dashboard needs in one call: daily volume, the
    /// hour-of-day pattern, outcome and disposition mix, and the agent table.
    ///
    /// Built over the call log rather than a provider's API, so it works
    /// identically whether calls were logged by hand or pushed in by a dialler.
    /// </summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<CallAnalyticsDto>> Analytics(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var window = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.Date.AddDays(-window + 1);

        var calls = await Db.CallLogs
            .Where(c => c.StartedAt >= since)
            .Select(c => new
            {
                c.StartedAt,
                c.Direction,
                c.Outcome,
                c.Disposition,
                c.DurationSeconds,
                c.WaitSeconds,
                c.SentimentLabel,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // Zero-fill the calendar: a day with no calls is a fact worth plotting,
        // and a gap in the series would misread as a flat line instead.
        var byDay = calls.GroupBy(c => c.StartedAt.Date).ToDictionary(g => g.Key, g => g.ToList());
        var daily = new List<CallDailyPointDto>();

        for (var day = since; day <= DateTime.UtcNow.Date; day = day.AddDays(1))
        {
            var onDay = byDay.GetValueOrDefault(day, []);
            daily.Add(new CallDailyPointDto(
                day.ToString("yyyy-MM-dd"),
                onDay.Count,
                onDay.Count(c => c.Outcome == CallOutcomes.Connected),
                Math.Round(onDay.Sum(c => c.DurationSeconds) / 60.0, 1)));
        }

        var hourly = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var inHour = calls.Where(c => c.StartedAt.Hour == hour).ToList();
                return new CallHourPointDto(
                    hour,
                    inHour.Count,
                    inHour.Count(c => c.Outcome == CallOutcomes.Connected));
            })
            .ToList();

        var connected = calls.Count(c => c.Outcome == CallOutcomes.Connected);
        var talkSeconds = calls.Sum(c => c.DurationSeconds);

        return Ok(new CallAnalyticsDto(
            window,
            calls.Count,
            connected,
            calls.Count == 0 ? 0 : Math.Round(connected * 100.0 / calls.Count, 1),
            Math.Round(talkSeconds / 60.0, 1),
            connected == 0 ? 0 : Math.Round(talkSeconds / (double)connected, 0),
            calls.Count(c => c.Direction == CallDirections.Inbound),
            calls.Count(c => c.Direction == CallDirections.Outbound),
            calls.Count(c => c.Direction == CallDirections.Missed),
            calls.Any(c => c.WaitSeconds is not null)
                ? Math.Round(calls.Where(c => c.WaitSeconds is not null).Average(c => c.WaitSeconds!.Value), 1)
                : 0,
            daily,
            hourly,
            calls.GroupBy(c => c.Outcome)
                .Select(g => new AgendaBucketDto(g.Key, Humanise(g.Key), g.Count()))
                .OrderByDescending(b => b.Count)
                .ToList(),
            calls.Where(c => c.Disposition is not null)
                .GroupBy(c => c.Disposition!)
                .Select(g => new AgendaBucketDto(g.Key, Humanise(g.Key), g.Count()))
                .OrderByDescending(b => b.Count)
                .ToList(),
            calls.Where(c => c.SentimentLabel is not null)
                .GroupBy(c => c.SentimentLabel!)
                .Select(g => new AgendaBucketDto(g.Key, g.Key, g.Count()))
                .ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CallLogDto>> GetOne(int id, CancellationToken ct)
    {
        var call = await Base().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Call");

        return Ok(ToDto(call));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<CallLogDto>> Create(CallLogInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);

        var agentId = input.AgentId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null);
        var agentName = agentId is null
            ? Db.Tenant.UserName
            : (await Db.Users.FindAsync([agentId], ct))?.Name ?? Db.Tenant.UserName;

        var call = new CallLog
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            AgentId = agentId,
            AgentName = agentName,
        };

        Apply(call, input);

        Db.CallLogs.Add(call);
        await Db.SaveChangesAsync(ct);

        await TouchRelatedAsync(call, ct);

        call.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = call.Id }, ToDto(call));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CallLogDto>> Update(int id, CallLogInput input, CancellationToken ct)
    {
        var call = await Db.CallLogs.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Call");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        call.BranchId = branch.Id;
        Apply(call, input);

        await Db.SaveChangesAsync(ct);

        call.Branch = branch;
        return Ok(ToDto(call));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var call = await Db.CallLogs.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Call");

        SoftDelete(call);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void Apply(CallLog call, CallLogInput input)
    {
        call.RelatedType = Require(input.RelatedType, RelatedTypes.All, "related object");
        call.RelatedId = input.RelatedId;
        call.RelatedName = input.RelatedName;
        call.Direction = Require(input.Direction, CallDirections.All, "direction");
        call.Outcome = Require(input.Outcome, CallOutcomes.All, "outcome");
        call.Disposition = input.Disposition is null
            ? null
            : Require(input.Disposition, CallDispositions.All, "disposition");
        call.PhoneNumber = input.PhoneNumber;
        call.StartedAt = input.StartedAt;
        call.DurationSeconds = Math.Max(0, input.DurationSeconds);
        call.WaitSeconds = input.WaitSeconds;
        call.Notes = input.Notes;
        call.FollowUpAt = input.FollowUpAt;

        var analysis = sentiment.Analyse(input.Notes);
        call.SentimentScore = analysis.Score;
        call.SentimentLabel = analysis.Label;
    }

    /// <summary>Logging a call is activity — the parent record's clock resets.</summary>
    private async Task TouchRelatedAsync(CallLog call, CancellationToken ct)
    {
        switch (call.RelatedType)
        {
            case RelatedTypes.Lead:
            {
                var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == call.RelatedId, ct);
                if (lead is null) return;

                lead.LastActivityAt = call.StartedAt;
                lead.FirstResponseAt ??= call.StartedAt;

                Db.LeadActivities.Add(new LeadActivity
                {
                    LeadId = lead.Id,
                    Type = LeadActivityTypes.Call,
                    Remarks = call.Notes ?? $"{call.Direction} call — {call.Outcome}.",
                    ActorId = call.AgentId ?? 0,
                    ActorName = call.AgentName,
                    CreatedAt = call.StartedAt,
                });
                break;
            }

            case RelatedTypes.Contact:
            {
                var contact = await Db.Contacts.FirstOrDefaultAsync(c => c.Id == call.RelatedId, ct);
                if (contact is not null) contact.LastActivityAt = call.StartedAt;
                break;
            }
        }

        await Db.SaveChangesAsync(ct);
    }
}

public record CallDailyPointDto(string Date, int Calls, int Connected, double TalkMinutes);

public record CallHourPointDto(int Hour, int Calls, int Connected);

public record CallAnalyticsDto(
    int WindowDays,
    int TotalCalls,
    int Connected,
    double ConnectRate,
    double TalkMinutes,
    double AverageCallSeconds,
    int Inbound,
    int Outbound,
    int Missed,
    double AverageWaitSeconds,
    IReadOnlyList<CallDailyPointDto> Daily,
    IReadOnlyList<CallHourPointDto> Hourly,
    IReadOnlyList<AgendaBucketDto> ByOutcome,
    IReadOnlyList<AgendaBucketDto> ByDisposition,
    IReadOnlyList<AgendaBucketDto> BySentiment
);

public record CallScorecardDto(
    int? AgentId,
    string AgentName,
    int Calls,
    int Connected,
    double ConnectRate,
    double TalkMinutes,
    double AverageCallSeconds,
    int Progressed,
    double ProgressionRate,
    int PositiveSentiment
);
