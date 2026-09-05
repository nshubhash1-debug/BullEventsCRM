using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Appraisals built on a template, feedback gathered around them, and what
/// came of a training.
///
/// The existing appraisal carried a self score and a manager score — two
/// numbers with nothing underneath. What was missing is the thing an appraisal
/// is for: an agreed set of responsibilities, scored one by one, so that the
/// conversation is about where the two of you disagree rather than about a
/// single number neither of you can defend.
/// </summary>
[ApiController]
[Route("api/hr/performance")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrPerformanceController(AppDbContext db) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Templates
     * ================================================================== */

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<HrAppraisalTemplateDto>>> Templates(
        CancellationToken ct)
    {
        var rows = await Db.HrAppraisalTemplates.AsNoTracking()
            .Include(t => t.Kras)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var used = await Db.HrAppraisals.AsNoTracking()
            .Where(a => a.AppraisalTemplateId != null)
            .GroupBy(a => a.AppraisalTemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, ct);

        return Ok(rows.Select(t => new HrAppraisalTemplateDto(
            t.Id, t.Name, t.Notes, t.IsActive,
            t.Kras.Sum(k => k.Weight),
            used.GetValueOrDefault(t.Id),
            t.Kras.OrderBy(k => k.SortOrder).Select(k => new HrTemplateKraDto(
                k.Id, k.Title, k.Description, k.Weight, k.SortOrder)).ToList())).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("templates")]
    public async Task<ActionResult<int>> SaveTemplate(
        [FromBody] HrAppraisalTemplateInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A template needs a name.");
        if (input.Kras.Count == 0)
            throw ApiException.BadRequest("A template with no responsibilities scores nothing.");

        var total = input.Kras.Sum(k => k.Weight);
        if (Math.Abs(total - 100m) > 0.01m)
        {
            // Weights that do not come to a hundred make every score on the
            // template incomparable with every other, which is the one thing a
            // template exists to prevent.
            throw ApiException.BadRequest(
                $"The weights come to {total:0.##}. They have to come to 100.");
        }

        if (input.Kras.Select(k => k.Title.Trim().ToLowerInvariant()).Distinct().Count()
            != input.Kras.Count)
        {
            throw ApiException.BadRequest("Two responsibilities share a title.");
        }

        var template = input.Id is int id and > 0
            ? await Db.HrAppraisalTemplates.Include(t => t.Kras)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw ApiException.NotFound("Appraisal template")
            : new HrAppraisalTemplate { CompanyId = Db.Tenant.CompanyId };

        template.Name = input.Name.Trim();
        template.Notes = input.Notes;
        template.IsActive = input.IsActive;

        if (template.Id == 0) Db.HrAppraisalTemplates.Add(template);

        Db.HrAppraisalTemplateKras.RemoveRange(template.Kras);
        template.Kras.Clear();

        var order = 0;
        foreach (var kra in input.Kras)
        {
            template.Kras.Add(new HrAppraisalTemplateKra
            {
                CompanyId = Db.Tenant.CompanyId,
                Title = kra.Title.Trim(),
                Description = kra.Description,
                Weight = kra.Weight,
                SortOrder = order,
            });
            order += 10;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(template.Id);
    }

    /* ================================================================== *
     * Appraisals
     * ================================================================== */

    /// <summary>
    /// Start an appraisal from a template, copying its responsibilities onto it.
    ///
    /// Copied rather than referenced so a template edited next year does not
    /// silently restate this year's rating against different weights.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("appraisals")]
    public async Task<ActionResult<HrAppraisalDetailDto>> StartAppraisal(
        [FromBody] HrStartAppraisalInput input, CancellationToken ct)
    {
        var cycle = await Db.HrAppraisalCycles.FirstOrDefaultAsync(c => c.Id == input.CycleId, ct)
            ?? throw ApiException.NotFound("Appraisal cycle");
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var existing = await Db.HrAppraisals.Include(a => a.Kras)
            .FirstOrDefaultAsync(a => a.CycleId == cycle.Id && a.EmployeeId == employee.Id, ct);
        if (existing is not null)
            throw ApiException.BadRequest($"{employee.Name} already has an appraisal in {cycle.Name}.");

        var template = await Db.HrAppraisalTemplates.AsNoTracking()
            .Include(t => t.Kras)
            .FirstOrDefaultAsync(t => t.Id == input.AppraisalTemplateId && t.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown appraisal template.");

        var appraisal = new HrAppraisal
        {
            CompanyId = Db.Tenant.CompanyId,
            CycleId = cycle.Id,
            EmployeeId = employee.Id,
            AppraisalTemplateId = template.Id,
            Status = "Draft",
        };

        foreach (var kra in template.Kras.OrderBy(k => k.SortOrder))
        {
            appraisal.Kras.Add(new HrAppraisalKra
            {
                CompanyId = Db.Tenant.CompanyId,
                Title = kra.Title,
                Weight = kra.Weight,
                SortOrder = kra.SortOrder,
            });
        }

        Db.HrAppraisals.Add(appraisal);
        await Db.SaveChangesAsync(ct);

        return Ok(await DetailAsync(appraisal.Id, ct));
    }

    [HttpGet("appraisals/{id:int}")]
    public async Task<ActionResult<HrAppraisalDetailDto>> Appraisal(int id, CancellationToken ct)
        => Ok(await DetailAsync(id, ct));

    /// <summary>
    /// Score the responsibilities.
    ///
    /// Self and manager scores are written by separate calls so a manager
    /// cannot overwrite what somebody said about themselves, and the weighted
    /// manager score is recomputed here rather than typed.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("appraisals/{id:int}/score")]
    public async Task<ActionResult<HrAppraisalDetailDto>> Score(
        int id, [FromQuery] bool asManager, [FromBody] HrAppraisalScoreInput input,
        CancellationToken ct)
    {
        var appraisal = await Db.HrAppraisals.Include(a => a.Kras)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Appraisal");

        if (appraisal.Status == "Closed")
            throw ApiException.BadRequest("That appraisal is closed.");

        if (input.Scores.Any(s => s.Score is < 1m or > 5m))
            throw ApiException.BadRequest("Scores run from one to five.");

        foreach (var score in input.Scores)
        {
            var row = appraisal.Kras.FirstOrDefault(k => k.Id == score.AppraisalKraId);
            if (row is null) continue;

            if (asManager)
            {
                row.ManagerScore = score.Score;
                row.ManagerComment = score.Comment;
            }
            else
            {
                row.SelfScore = score.Score;
                row.SelfComment = score.Comment;
            }
        }

        // Weighted over the responsibilities that have been answered, so a
        // half-finished appraisal shows a score for what was scored rather than
        // one dragged down by blanks.
        appraisal.SelfScore = Weighted(appraisal.Kras, k => k.SelfScore);
        appraisal.ManagerScore = Weighted(appraisal.Kras, k => k.ManagerScore);
        appraisal.FinalScore = appraisal.ManagerScore;

        if (!string.IsNullOrWhiteSpace(input.Comments))
        {
            appraisal.Comments = input.Comments;
        }
        if (appraisal.Status == "Draft") appraisal.Status = "InProgress";

        await Db.SaveChangesAsync(ct);
        return Ok(await DetailAsync(id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("appraisals/{id:int}/close")]
    public async Task<ActionResult<HrAppraisalDetailDto>> Close(
        int id, [FromQuery] string? rating, CancellationToken ct)
    {
        var appraisal = await Db.HrAppraisals.Include(a => a.Kras)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Appraisal");

        var unscored = appraisal.Kras.Count(k => k.ManagerScore is null);
        if (unscored > 0)
        {
            throw ApiException.BadRequest(
                $"{unscored} responsibilit{(unscored == 1 ? "y has" : "ies have")} no manager "
                + "score. An appraisal closed with blanks is one nobody can stand behind.");
        }

        appraisal.Rating = rating;
        appraisal.Status = "Closed";
        await Db.SaveChangesAsync(ct);
        return Ok(await DetailAsync(id, ct));
    }

    private static decimal? Weighted(
        IEnumerable<HrAppraisalKra> kras, Func<HrAppraisalKra, decimal?> pick)
    {
        var scored = kras.Where(k => pick(k) is not null).ToList();
        if (scored.Count == 0) return null;

        var weights = scored.Sum(k => k.Weight);
        if (weights == 0m) return null;

        return Math.Round(scored.Sum(k => pick(k)!.Value * k.Weight) / weights, 2);
    }

    private async Task<HrAppraisalDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var appraisal = await Db.HrAppraisals.AsNoTracking()
            .Include(a => a.Kras).Include(a => a.Employee).Include(a => a.Cycle)
            .Include(a => a.AppraisalTemplate)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Appraisal");

        var feedback = await Db.HrPerformanceFeedbacks.AsNoTracking()
            .Where(f => f.EmployeeId == appraisal.EmployeeId
                && f.AppraisalCycleId == appraisal.CycleId)
            .ToListAsync(ct);

        return new HrAppraisalDetailDto(
            appraisal.Id, appraisal.CycleId, appraisal.Cycle?.Name ?? "—",
            appraisal.EmployeeId, appraisal.Employee?.Name ?? "—",
            appraisal.AppraisalTemplateId, appraisal.AppraisalTemplate?.Name,
            appraisal.SelfScore, appraisal.ManagerScore, appraisal.FinalScore,
            appraisal.Rating, appraisal.Comments, appraisal.Status,
            feedback.Count,
            feedback.Where(f => f.Rating is not null).Select(f => f.Rating!.Value).ToList() is
                { Count: > 0 } ratings
                ? Math.Round(ratings.Average(), 2)
                : null,
            appraisal.Kras.OrderBy(k => k.SortOrder).Select(k => new HrAppraisalKraDto(
                k.Id, k.Title, k.Weight, k.SelfScore, k.ManagerScore,
                k.SelfComment, k.ManagerComment,
                // The gap between what somebody said about themselves and what
                // their manager said is the conversation the appraisal is for.
                k.SelfScore is decimal self && k.ManagerScore is decimal manager
                    ? Math.Round(manager - self, 2)
                    : null)).ToList());
    }

    /* ================================================================== *
     * Feedback
     * ================================================================== */

    [HttpGet("feedback")]
    public async Task<ActionResult<IReadOnlyList<HrFeedbackDto>>> Feedback(
        [FromQuery] int? employeeId, [FromQuery] int? cycleId,
        [FromQuery] bool includeUnshared, CancellationToken ct)
    {
        var query = Db.HrPerformanceFeedbacks.AsNoTracking()
            .Include(f => f.Employee).Include(f => f.GivenByEmployee)
            .AsQueryable();

        if (employeeId is int id) query = query.Where(f => f.EmployeeId == id);
        if (cycleId is int cycle) query = query.Where(f => f.AppraisalCycleId == cycle);
        if (!includeUnshared) query = query.Where(f => f.SharedWithEmployee);

        var rows = await query.OrderByDescending(f => f.Id).Take(300).ToListAsync(ct);

        return Ok(rows.Select(f => new HrFeedbackDto(
            f.Id, f.EmployeeId, f.Employee?.Name ?? "—",
            f.AppraisalCycleId,
            // Anonymity is honoured on the way out, not by refusing to store
            // the author — somebody has to be able to investigate abuse.
            f.IsAnonymous ? null : f.GivenByEmployeeId,
            f.IsAnonymous ? "Anonymous" : f.GivenByEmployee?.Name ?? "—",
            f.Relationship, f.Rating, f.WhatWorksWell, f.WhatCouldImprove,
            f.IsAnonymous, f.SharedWithEmployee, f.CreatedAt)).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("feedback")]
    public async Task<ActionResult<HrFeedbackDto>> GiveFeedback(
        [FromBody] HrFeedbackInput input, CancellationToken ct)
    {
        if (input.EmployeeId == input.GivenByEmployeeId)
        {
            throw ApiException.BadRequest(
                "Feedback about yourself is the self assessment, which lives on the appraisal.");
        }
        if (!FeedbackRelationships.All.Contains(input.Relationship))
            throw ApiException.BadRequest("Unknown relationship.");
        if (input.Rating is < 1m or > 5m)
            throw ApiException.BadRequest("Ratings run from one to five.");
        if (string.IsNullOrWhiteSpace(input.WhatWorksWell)
            && string.IsNullOrWhiteSpace(input.WhatCouldImprove))
        {
            throw ApiException.BadRequest("Write something — a bare rating helps nobody.");
        }

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.GivenByEmployeeId, ct)
            ?? throw ApiException.NotFound("Employee giving the feedback");

        var row = new HrPerformanceFeedback
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            GivenByEmployeeId = input.GivenByEmployeeId,
            AppraisalCycleId = input.AppraisalCycleId,
            Relationship = input.Relationship,
            Rating = input.Rating,
            WhatWorksWell = input.WhatWorksWell,
            WhatCouldImprove = input.WhatCouldImprove,
            IsAnonymous = input.IsAnonymous,
            SharedWithEmployee = false,
        };
        Db.HrPerformanceFeedbacks.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(f => f.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(f => f.GivenByEmployee).LoadAsync(ct);

        return Ok(new HrFeedbackDto(
            row.Id, row.EmployeeId, row.Employee?.Name ?? "—", row.AppraisalCycleId,
            row.IsAnonymous ? null : row.GivenByEmployeeId,
            row.IsAnonymous ? "Anonymous" : row.GivenByEmployee?.Name ?? "—",
            row.Relationship, row.Rating, row.WhatWorksWell, row.WhatCouldImprove,
            row.IsAnonymous, row.SharedWithEmployee, row.CreatedAt));
    }

    /// <summary>
    /// Release feedback to the person it is about.
    ///
    /// Held back until somebody decides, because 360 feedback arriving
    /// unmediated is how it stops being given.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("feedback/share")]
    public async Task<ActionResult<int>> ShareFeedback(
        [FromQuery] int employeeId, [FromQuery] int? cycleId, CancellationToken ct)
    {
        var rows = await Db.HrPerformanceFeedbacks
            .Where(f => f.EmployeeId == employeeId && !f.SharedWithEmployee
                && (cycleId == null || f.AppraisalCycleId == cycleId))
            .ToListAsync(ct);

        foreach (var row in rows) row.SharedWithEmployee = true;
        await Db.SaveChangesAsync(ct);
        return Ok(rows.Count);
    }

    /* ================================================================== *
     * Training outcomes
     * ================================================================== */

    /// <summary>Who attended a training, what came of it, and what they made of it.</summary>
    [HttpGet("trainings/{trainingId:int}/outcome")]
    public async Task<ActionResult<HrTrainingOutcomeDto>> TrainingOutcome(
        int trainingId, CancellationToken ct)
    {
        var training = await Db.HrTrainings.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct)
            ?? throw ApiException.NotFound("Training");

        var enrolments = await Db.HrTrainingEnrolments.AsNoTracking()
            .Include(e => e.Employee)
            .Where(e => e.TrainingId == trainingId)
            .OrderBy(e => e.Employee!.Name)
            .ToListAsync(ct);

        var feedback = await Db.HrTrainingFeedbacks.AsNoTracking()
            .Include(f => f.Employee)
            .Where(f => f.TrainingId == trainingId)
            .ToListAsync(ct);

        var ratings = feedback.Select(f => f.Rating).ToList();

        return Ok(new HrTrainingOutcomeDto(
            training.Id, training.Title, training.Trainer,
            training.FromDate, training.ToDate, training.Status,
            enrolments.Count,
            enrolments.Count(e => e.Result == TrainingResults.Passed),
            enrolments.Count(e => e.Result == TrainingResults.Failed),
            enrolments.Count(e => e.Result == TrainingResults.Absent),
            ratings.Count > 0 ? Math.Round(ratings.Average(), 2) : null,
            feedback.Count > 0
                ? Math.Round(100m * feedback.Count(f => f.WouldRecommend) / feedback.Count, 0)
                : null,
            enrolments.Select(e => new HrEnrolmentOutcomeDto(
                e.Id, e.EmployeeId, e.Employee?.Name ?? "—",
                e.Status, e.Result, e.Score, e.TrainerRemarks,
                feedback.Any(f => f.EmployeeId == e.EmployeeId))).ToList(),
            feedback.Select(f => new HrTrainingFeedbackDto(
                f.Id, f.EmployeeId, f.Employee?.Name ?? "—",
                f.Rating, f.WouldRecommend, f.Comments, f.CreatedAt)).ToList()));
    }

    /// <summary>The trainer's verdict on an attendee.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("enrolments/{id:int}/result")]
    public async Task<IActionResult> RecordResult(
        int id, [FromBody] HrTrainingResultInput input, CancellationToken ct)
    {
        if (!TrainingResults.All.Contains(input.Result))
            throw ApiException.BadRequest("Unknown training result.");

        var enrolment = await Db.HrTrainingEnrolments.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw ApiException.NotFound("Enrolment");

        enrolment.Result = input.Result;
        enrolment.Score = input.Score;
        enrolment.TrainerRemarks = input.TrainerRemarks;
        enrolment.Status = input.Result == TrainingResults.Absent ? "Absent" : "Completed";

        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>What an attendee made of the training — the other half of the question.</summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("trainings/{trainingId:int}/feedback")]
    public async Task<ActionResult<HrTrainingFeedbackDto>> TrainingFeedback(
        int trainingId, [FromBody] HrTrainingFeedbackInput input, CancellationToken ct)
    {
        if (input.Rating is < 1m or > 5m)
            throw ApiException.BadRequest("Ratings run from one to five.");

        var attended = await Db.HrTrainingEnrolments.AnyAsync(
            e => e.TrainingId == trainingId && e.EmployeeId == input.EmployeeId, ct);
        if (!attended)
            throw ApiException.BadRequest("That person was not enrolled on this training.");

        var existing = await Db.HrTrainingFeedbacks.FirstOrDefaultAsync(
            f => f.TrainingId == trainingId && f.EmployeeId == input.EmployeeId, ct);

        var row = existing ?? new HrTrainingFeedback
        {
            CompanyId = Db.Tenant.CompanyId,
            TrainingId = trainingId,
            EmployeeId = input.EmployeeId,
        };

        row.Rating = input.Rating;
        row.WouldRecommend = input.WouldRecommend;
        row.Comments = input.Comments;

        if (existing is null) Db.HrTrainingFeedbacks.Add(row);
        await Db.SaveChangesAsync(ct);
        await Db.Entry(row).Reference(f => f.Employee).LoadAsync(ct);

        return Ok(new HrTrainingFeedbackDto(
            row.Id, row.EmployeeId, row.Employee?.Name ?? "—",
            row.Rating, row.WouldRecommend, row.Comments, row.CreatedAt));
    }
}
