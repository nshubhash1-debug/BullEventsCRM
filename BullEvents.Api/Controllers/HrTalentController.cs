using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Hiring and performance, with the decisions kept.
///
/// The existing recruitment endpoints move a candidate through stages; this
/// one is everywhere a judgement gets recorded — who agreed the headcount, who
/// asked for the role, what each panellist thought, what was offered and why
/// it was turned down, who brought the person in, and how the year went.
/// </summary>
[ApiController]
[Route("api/hr/talent")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrTalentController(AppDbContext db) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Staffing plan
     * ================================================================== */

    [HttpGet("staffing-plans")]
    public async Task<ActionResult<IReadOnlyList<HrStaffingPlanDto>>> StaffingPlans(
        CancellationToken ct)
    {
        var plans = await Db.HrStaffingPlans.AsNoTracking()
            .Include(p => p.Lines).ThenInclude(l => l.Department)
            .OrderByDescending(p => p.FromDate).ToListAsync(ct);

        // What each line has already been spent on, so a plan shows its own
        // remaining headcount rather than only what was agreed.
        var used = await Db.HrJobRequisitions.AsNoTracking()
            .Where(r => r.StaffingPlanId != null && !r.IsReplacement
                && (r.Status == RequisitionStatuses.Approved
                    || r.Status == RequisitionStatuses.Filled))
            .GroupBy(r => new { r.StaffingPlanId, r.DepartmentId, r.Position })
            .Select(g => new
            {
                g.Key.StaffingPlanId,
                g.Key.DepartmentId,
                g.Key.Position,
                Headcount = g.Sum(r => r.Headcount),
            })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return Ok(plans.Select(p => new HrStaffingPlanDto(
            p.Id, p.Name, p.FromDate, p.ToDate, p.Status, p.Notes, p.Covers(today),
            p.Lines.Sum(l => l.Headcount),
            p.Lines.Sum(l => l.Headcount * l.BudgetPerHead),
            p.Lines.OrderBy(l => l.Position).Select(l =>
            {
                var taken = used
                    .Where(u => u.StaffingPlanId == p.Id
                        && u.DepartmentId == l.DepartmentId
                        && u.Position == l.Position)
                    .Sum(u => u.Headcount);

                return new HrStaffingPlanLineDto(
                    l.Id, l.DepartmentId, l.Department?.Name, l.Position,
                    l.Headcount, l.BudgetPerHead, l.Notes,
                    taken, Math.Max(0, l.Headcount - taken));
            }).ToList())).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("staffing-plans")]
    public async Task<ActionResult<int>> SaveStaffingPlan(
        [FromBody] HrStaffingPlanInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A plan needs a name.");
        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("A plan has to end on or after it starts.");
        if (input.Lines.Any(l => l.Headcount <= 0))
            throw ApiException.BadRequest("A line asking for nobody is not a line.");

        var plan = input.Id is int id and > 0
            ? await Db.HrStaffingPlans.Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw ApiException.NotFound("Staffing plan")
            : new HrStaffingPlan { CompanyId = Db.Tenant.CompanyId };

        plan.Name = input.Name.Trim();
        plan.FromDate = input.FromDate;
        plan.ToDate = input.ToDate;
        plan.Status = input.Status;
        plan.Notes = input.Notes;

        if (plan.Id == 0) Db.HrStaffingPlans.Add(plan);

        Db.HrStaffingPlanLines.RemoveRange(plan.Lines);
        plan.Lines.Clear();

        foreach (var line in input.Lines)
        {
            plan.Lines.Add(new HrStaffingPlanLine
            {
                CompanyId = Db.Tenant.CompanyId,
                DepartmentId = line.DepartmentId,
                Position = line.Position.Trim(),
                Headcount = line.Headcount,
                BudgetPerHead = line.BudgetPerHead,
                Notes = line.Notes,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(plan.Id);
    }

    /* ================================================================== *
     * Requisitions
     * ================================================================== */

    [HttpGet("requisitions")]
    public async Task<ActionResult<IReadOnlyList<HrRequisitionDto>>> Requisitions(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrJobRequisitions.AsNoTracking()
            .Include(r => r.Department).Include(r => r.RequestedByEmployee)
            .Include(r => r.StaffingPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.Id).Take(300).ToListAsync(ct);
        return Ok(rows.Select(ToRequisition).ToList());
    }

    /// <summary>
    /// Ask for a role to be opened.
    ///
    /// Checked against the staffing plan covering the required-by date. A
    /// replacement is let through regardless — the post was already budgeted,
    /// and counting it against the plan again is how a plan appears breached
    /// when it is not.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("requisitions")]
    public async Task<ActionResult<HrRequisitionDto>> Raise(
        [FromBody] HrRequisitionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Position))
            throw ApiException.BadRequest("Say what the role is.");
        if (input.Headcount <= 0)
            throw ApiException.BadRequest("Ask for at least one person.");
        if (input.SalaryMin is decimal min && input.SalaryMax is decimal max && max < min)
            throw ApiException.BadRequest("The top of the range is below the bottom of it.");
        if (input.IsReplacement && input.ReplacingEmployeeId is null)
            throw ApiException.BadRequest("Say who is being replaced.");

        var row = new HrJobRequisition
        {
            CompanyId = Db.Tenant.CompanyId,
            Position = input.Position.Trim(),
            DepartmentId = input.DepartmentId,
            DesignationId = input.DesignationId,
            Headcount = input.Headcount,
            IsReplacement = input.IsReplacement,
            ReplacingEmployeeId = input.ReplacingEmployeeId,
            RequestedByEmployeeId = input.RequestedByEmployeeId,
            RequiredBy = input.RequiredBy,
            Justification = input.Justification,
            JobDescription = input.JobDescription,
            SalaryMin = input.SalaryMin,
            SalaryMax = input.SalaryMax,
            Location = input.Location,
            Status = RequisitionStatuses.PendingApproval,
        };

        var (plan, shortfall) = await CheckAgainstPlanAsync(row, ct);
        row.StaffingPlanId = plan?.Id;

        Db.HrJobRequisitions.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.Department).LoadAsync(ct);
        await Db.Entry(row).Reference(r => r.RequestedByEmployee).LoadAsync(ct);
        await Db.Entry(row).Reference(r => r.StaffingPlan).LoadAsync(ct);

        var dto = ToRequisition(row) with { PlanWarning = shortfall };
        return Ok(dto);
    }

    /// <summary>
    /// Approve or refuse. Approving opens the vacancy.
    ///
    /// The vacancy is created here rather than left to somebody to remember,
    /// because a requisition approved and never advertised is the commonest
    /// way a role goes unfilled while everybody believes it is open.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("requisitions/{id:int}/decide")]
    public async Task<ActionResult<HrRequisitionDto>> DecideRequisition(
        int id, [FromQuery] bool approve, [FromQuery] string? note, CancellationToken ct)
    {
        var row = await Db.HrJobRequisitions
            .Include(r => r.Department).Include(r => r.RequestedByEmployee)
            .Include(r => r.StaffingPlan)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Requisition");

        if (row.Status is not RequisitionStatuses.PendingApproval and not RequisitionStatuses.Draft)
            throw ApiException.BadRequest("That requisition has already been decided.");

        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(note))
                throw ApiException.BadRequest("Say why it is being refused — the asker will want to know.");

            row.Status = RequisitionStatuses.Rejected;
            row.DecisionNote = note;
            row.DecidedAt = DateTime.UtcNow;
            await Db.SaveChangesAsync(ct);
            return Ok(ToRequisition(row));
        }

        var vacancy = new HrVacancy
        {
            CompanyId = Db.Tenant.CompanyId,
            Position = row.Position,
            DepartmentId = row.DepartmentId,
            Location = row.Location,
            SalaryMin = row.SalaryMin,
            SalaryMax = row.SalaryMax,
            JobDescription = row.JobDescription,
            HiringManagerEmployeeId = row.RequestedByEmployeeId,
            Status = "Open",
        };
        Db.HrVacancies.Add(vacancy);

        row.Status = RequisitionStatuses.Approved;
        row.DecisionNote = note;
        row.DecidedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);

        row.VacancyId = vacancy.Id;
        await Db.SaveChangesAsync(ct);

        return Ok(ToRequisition(row));
    }

    /// <summary>
    /// Whether a requisition fits inside the plan, and which plan it was read
    /// against. Returns the plan and a warning, not an error — a plan is
    /// guidance, and refusing outright would only teach people to stop
    /// recording plans.
    /// </summary>
    private async Task<(HrStaffingPlan? Plan, string? Warning)> CheckAgainstPlanAsync(
        HrJobRequisition row, CancellationToken ct)
    {
        if (row.IsReplacement) return (null, null);

        var against = row.RequiredBy ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await Db.HrStaffingPlans.AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.FromDate <= against && p.ToDate >= against)
            .OrderByDescending(p => p.FromDate)
            .FirstOrDefaultAsync(ct);

        if (plan is null) return (null, "No staffing plan covers that date.");

        var line = plan.Lines.FirstOrDefault(l =>
            l.DepartmentId == row.DepartmentId
            && string.Equals(l.Position, row.Position, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return (plan, $"{plan.Name} does not budget for a {row.Position} "
                + "in that department.");
        }

        var taken = await Db.HrJobRequisitions.AsNoTracking()
            .Where(r => r.StaffingPlanId == plan.Id && !r.IsReplacement
                && r.DepartmentId == row.DepartmentId
                && r.Position == row.Position
                && (r.Status == RequisitionStatuses.Approved
                    || r.Status == RequisitionStatuses.Filled))
            .SumAsync(r => (int?)r.Headcount, ct) ?? 0;

        var remaining = line.Headcount - taken;
        if (row.Headcount > remaining)
        {
            return (plan, $"{plan.Name} allows {line.Headcount} and {taken} "
                + $"{(taken == 1 ? "has" : "have")} been approved, so this asks for "
                + $"{row.Headcount - remaining} more than the plan.");
        }

        return (plan, null);
    }

    private static HrRequisitionDto ToRequisition(HrJobRequisition r) => new(
        r.Id, r.Position, r.DepartmentId, r.Department?.Name, r.Headcount,
        r.IsReplacement, r.ReplacingEmployeeId,
        r.RequestedByEmployeeId, r.RequestedByEmployee?.Name,
        r.RequiredBy, r.Justification, r.JobDescription,
        r.SalaryMin, r.SalaryMax, r.Location,
        r.StaffingPlanId, r.StaffingPlan?.Name,
        r.Status, r.DecisionNote, r.DecidedAt, r.VacancyId, null);

    /* ================================================================== *
     * Interview rounds and scorecards
     * ================================================================== */

    [HttpGet("interview-rounds")]
    public async Task<ActionResult<IReadOnlyList<HrInterviewRoundDto>>> Rounds(
        CancellationToken ct)
    {
        var rows = await Db.HrInterviewRounds.AsNoTracking()
            .Include(r => r.Skills)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(r => new HrInterviewRoundDto(
            r.Id, r.Name, r.SortOrder, r.PassingScore, r.Notes, r.IsActive,
            r.Skills.OrderBy(s => s.SortOrder).Select(s => new HrInterviewSkillDto(
                s.Id, s.Name, s.Weight, s.SortOrder)).ToList())).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("interview-rounds")]
    public async Task<ActionResult<int>> SaveRound(
        [FromBody] HrInterviewRoundInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A round needs a name.");
        if (input.Skills.Count == 0)
        {
            throw ApiException.BadRequest(
                "A round with nothing to assess is a conversation. Add at least one skill.");
        }
        if (input.Skills.Select(s => s.Name.Trim().ToLowerInvariant()).Distinct().Count()
            != input.Skills.Count)
        {
            throw ApiException.BadRequest("Two skills on the round share a name.");
        }

        var round = input.Id is int id and > 0
            ? await Db.HrInterviewRounds.Include(r => r.Skills)
                .FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw ApiException.NotFound("Interview round")
            : new HrInterviewRound { CompanyId = Db.Tenant.CompanyId };

        round.Name = input.Name.Trim();
        round.SortOrder = input.SortOrder;
        round.PassingScore = input.PassingScore;
        round.Notes = input.Notes;
        round.IsActive = input.IsActive;

        if (round.Id == 0) Db.HrInterviewRounds.Add(round);

        Db.HrInterviewSkills.RemoveRange(round.Skills);
        round.Skills.Clear();

        var order = 0;
        foreach (var skill in input.Skills)
        {
            round.Skills.Add(new HrInterviewSkill
            {
                CompanyId = Db.Tenant.CompanyId,
                Name = skill.Name.Trim(),
                Weight = skill.Weight > 0m ? skill.Weight : 1m,
                SortOrder = order,
            });
            order += 10;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(round.Id);
    }

    /// <summary>Every panellist's scorecard for one interview, and where they disagree.</summary>
    [HttpGet("interviews/{interviewId:int}/feedback")]
    public async Task<ActionResult<HrInterviewPanelDto>> Panel(
        int interviewId, CancellationToken ct)
    {
        var interview = await Db.HrInterviews.AsNoTracking()
            .Include(i => i.Candidate).Include(i => i.InterviewRound)
            .FirstOrDefaultAsync(i => i.Id == interviewId, ct)
            ?? throw ApiException.NotFound("Interview");

        var feedback = await Db.HrInterviewFeedbacks.AsNoTracking()
            .Include(f => f.Ratings).Include(f => f.PanellistEmployee)
            .Where(f => f.InterviewId == interviewId)
            .OrderBy(f => f.Id)
            .ToListAsync(ct);

        var scores = feedback.Select(f => f.Score).ToList();

        return Ok(new HrInterviewPanelDto(
            interview.Id,
            interview.Candidate?.Name ?? "—",
            interview.InterviewRoundId,
            interview.InterviewRound?.Name,
            interview.InterviewRound?.PassingScore,
            feedback.Count,
            scores.Count > 0 ? Math.Round(scores.Average(), 2) : null,
            // The spread is the point of a panel: three fours and a set of
            // five, three, four are not the same candidate, and an average
            // hides which one is in front of you.
            scores.Count > 1 ? Math.Round(scores.Max() - scores.Min(), 2) : null,
            feedback.Select(f => new HrInterviewFeedbackDto(
                f.Id, f.PanellistEmployeeId, f.PanellistName, f.Score,
                f.Recommendation, f.Strengths, f.Concerns,
                f.Ratings.OrderBy(r => r.Id).Select(r => new HrSkillRatingDto(
                    r.SkillName, r.Rating, r.Weight, r.Comment)).ToList())).ToList()));
    }

    /// <summary>
    /// A panellist's scorecard.
    ///
    /// The overall score is computed from the skill ratings rather than typed,
    /// so it cannot disagree with the detail beneath it.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("interviews/{interviewId:int}/feedback")]
    public async Task<ActionResult<HrInterviewFeedbackDto>> GiveFeedback(
        int interviewId, [FromBody] HrInterviewFeedbackInput input, CancellationToken ct)
    {
        var interview = await Db.HrInterviews
            .Include(i => i.InterviewRound).ThenInclude(r => r!.Skills)
            .FirstOrDefaultAsync(i => i.Id == interviewId, ct)
            ?? throw ApiException.NotFound("Interview");

        if (input.Ratings.Count == 0)
            throw ApiException.BadRequest("Rate at least one skill.");
        if (input.Ratings.Any(r => r.Rating is < 1m or > 5m))
            throw ApiException.BadRequest("Ratings run from one to five.");

        var name = (input.PanellistName ?? string.Empty).Trim();
        if (input.PanellistEmployeeId is int employeeId)
        {
            var panellist = await Db.HrEmployees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId, ct);
            if (panellist is not null) name = panellist.Name;
        }
        if (name.Length == 0)
            throw ApiException.BadRequest("Say who gave this feedback.");

        var existing = await Db.HrInterviewFeedbacks
            .Include(f => f.Ratings)
            .FirstOrDefaultAsync(f => f.InterviewId == interviewId && f.PanellistName == name, ct);

        var feedback = existing ?? new HrInterviewFeedback
        {
            CompanyId = Db.Tenant.CompanyId,
            InterviewId = interviewId,
            PanellistName = name,
        };

        feedback.PanellistEmployeeId = input.PanellistEmployeeId;
        feedback.Recommendation = input.Recommendation;
        feedback.Strengths = input.Strengths;
        feedback.Concerns = input.Concerns;

        if (existing is null) Db.HrInterviewFeedbacks.Add(feedback);
        else
        {
            Db.HrInterviewSkillRatings.RemoveRange(feedback.Ratings);
            feedback.Ratings.Clear();
        }

        var skills = interview.InterviewRound?.Skills.ToList() ?? [];
        decimal weighted = 0m;
        decimal weights = 0m;

        foreach (var rating in input.Ratings)
        {
            var skill = skills.FirstOrDefault(s => s.Id == rating.InterviewSkillId);
            var skillName = skill?.Name
                ?? (string.IsNullOrWhiteSpace(rating.SkillName) ? "Overall" : rating.SkillName.Trim());
            var weight = skill?.Weight ?? (rating.Weight > 0m ? rating.Weight : 1m);

            feedback.Ratings.Add(new HrInterviewSkillRating
            {
                CompanyId = Db.Tenant.CompanyId,
                InterviewSkillId = skill?.Id,
                SkillName = skillName,
                Rating = rating.Rating,
                Weight = weight,
                Comment = rating.Comment,
            });

            weighted += rating.Rating * weight;
            weights += weight;
        }

        feedback.Score = weights == 0m ? 0m : Math.Round(weighted / weights, 2);

        // The interview's own score becomes the panel average, so the pipeline
        // list keeps working without knowing about scorecards.
        await Db.SaveChangesAsync(ct);

        var panelScores = await Db.HrInterviewFeedbacks.AsNoTracking()
            .Where(f => f.InterviewId == interviewId)
            .Select(f => f.Score)
            .ToListAsync(ct);

        interview.Score = panelScores.Count > 0
            ? Math.Round(panelScores.Average(), 2)
            : null;
        await Db.SaveChangesAsync(ct);

        return Ok(new HrInterviewFeedbackDto(
            feedback.Id, feedback.PanellistEmployeeId, feedback.PanellistName,
            feedback.Score, feedback.Recommendation, feedback.Strengths, feedback.Concerns,
            feedback.Ratings.Select(r => new HrSkillRatingDto(
                r.SkillName, r.Rating, r.Weight, r.Comment)).ToList()));
    }

    /* ================================================================== *
     * Offers
     * ================================================================== */

    [HttpGet("offers")]
    public async Task<ActionResult<IReadOnlyList<HrJobOfferDto>>> Offers(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrJobOffers.AsNoTracking().Include(o => o.Candidate).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

        var rows = await query.OrderByDescending(o => o.Id).Take(300).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(rows.Select(o => ToOffer(o, today)).ToList());
    }

    /// <summary>
    /// Make an offer.
    ///
    /// A second offer to the same candidate is a new revision rather than an
    /// edit — a renegotiation is a fact about how the hire went, and
    /// overwriting the first figure loses it.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("offers")]
    public async Task<ActionResult<HrJobOfferDto>> MakeOffer(
        [FromBody] HrJobOfferInput input, CancellationToken ct)
    {
        var candidate = await Db.HrCandidates.FirstOrDefaultAsync(c => c.Id == input.CandidateId, ct)
            ?? throw ApiException.NotFound("Candidate");

        if (input.AnnualCtc <= 0m)
            throw ApiException.BadRequest("An offer needs a figure.");
        if (input.ValidUntil is DateOnly until && until < input.OfferDate)
            throw ApiException.BadRequest("The offer expires before it is made.");

        var open = await Db.HrJobOffers
            .Where(o => o.CandidateId == candidate.Id
                && (o.Status == OfferStatuses.Draft || o.Status == OfferStatuses.Sent))
            .ToListAsync(ct);

        // A live offer is superseded rather than left standing beside the new
        // one, or the candidate holds two.
        foreach (var previous in open)
        {
            previous.Status = OfferStatuses.Withdrawn;
            previous.OutcomeReason = "Superseded by a revised offer";
        }

        var revision = await Db.HrJobOffers
            .Where(o => o.CandidateId == candidate.Id)
            .MaxAsync(o => (int?)o.Revision, ct) ?? 0;

        var offer = new HrJobOffer
        {
            CompanyId = Db.Tenant.CompanyId,
            CandidateId = candidate.Id,
            Position = input.Position?.Trim() is { Length: > 0 } position
                ? position
                : candidate.Vacancy?.Position ?? "—",
            DepartmentId = input.DepartmentId,
            DesignationId = input.DesignationId,
            AnnualCtc = input.AnnualCtc,
            PayStructureId = input.PayStructureId,
            OfferDate = input.OfferDate,
            ValidUntil = input.ValidUntil,
            ProposedJoiningDate = input.ProposedJoiningDate,
            Terms = input.Terms,
            Status = OfferStatuses.Draft,
            Revision = revision + 1,
        };
        Db.HrJobOffers.Add(offer);

        candidate.Stage = RecruitmentStages.Offered;
        candidate.OfferSalary = input.AnnualCtc;
        if (input.ProposedJoiningDate is DateOnly joining)
        {
            candidate.OfferJoiningDate = joining.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        await Db.SaveChangesAsync(ct);
        await Db.Entry(offer).Reference(o => o.Candidate).LoadAsync(ct);

        return Ok(ToOffer(offer, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    /// <summary>
    /// Move an offer along. Declining wants a reason — it is the most useful
    /// field in recruitment and the one nobody fills in unless asked.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("offers/{id:int}/status")]
    public async Task<ActionResult<HrJobOfferDto>> SetOfferStatus(
        int id, [FromQuery] string status, [FromQuery] string? reason, CancellationToken ct)
    {
        if (!OfferStatuses.All.Contains(status))
            throw ApiException.BadRequest("Unknown offer status.");

        var offer = await Db.HrJobOffers.Include(o => o.Candidate)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Offer");

        if (offer.Status is OfferStatuses.Accepted or OfferStatuses.Declined)
            throw ApiException.BadRequest("That offer has already been answered.");

        if (status is OfferStatuses.Declined or OfferStatuses.Withdrawn
            && string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.BadRequest(
                "Say why. A declined offer without a reason teaches nobody anything.");
        }

        offer.Status = status;
        offer.OutcomeReason = reason;
        if (status is OfferStatuses.Accepted or OfferStatuses.Declined)
        {
            offer.RespondedAt = DateTime.UtcNow;
        }

        if (offer.Candidate is not null)
        {
            offer.Candidate.Stage = status switch
            {
                OfferStatuses.Accepted => RecruitmentStages.Selected,
                OfferStatuses.Declined => RecruitmentStages.Rejected,
                _ => offer.Candidate.Stage,
            };
        }

        await Db.SaveChangesAsync(ct);
        return Ok(ToOffer(offer, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    private static HrJobOfferDto ToOffer(HrJobOffer o, DateOnly today) => new(
        o.Id, o.CandidateId, o.Candidate?.Name ?? "—", o.Position,
        o.AnnualCtc, o.OfferDate, o.ValidUntil, o.ProposedJoiningDate,
        o.Status, o.OutcomeReason, o.RespondedAt, o.Terms, o.Revision,
        o.HasLapsed(today));

    /* ================================================================== *
     * Referrals
     * ================================================================== */

    [HttpGet("referrals")]
    public async Task<ActionResult<IReadOnlyList<HrReferralDto>>> Referrals(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrEmployeeReferrals.AsNoTracking()
            .Include(r => r.ReferrerEmployee).Include(r => r.Candidate).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.Id).Take(300).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(rows.Select(r => ToReferral(r, today)).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("referrals")]
    public async Task<ActionResult<HrReferralDto>> Refer(
        [FromBody] HrReferralInput input, CancellationToken ct)
    {
        var referrer = await Db.HrEmployees.FirstOrDefaultAsync(
            e => e.Id == input.ReferrerEmployeeId, ct)
            ?? throw ApiException.NotFound("Referring employee");

        if (string.IsNullOrWhiteSpace(input.CandidateName))
            throw ApiException.BadRequest("Say who is being referred.");

        var row = new HrEmployeeReferral
        {
            CompanyId = Db.Tenant.CompanyId,
            ReferrerEmployeeId = referrer.Id,
            CandidateName = input.CandidateName.Trim(),
            Phone = input.Phone,
            Email = input.Email,
            Position = input.Position,
            Notes = input.Notes,
            VacancyId = input.VacancyId,
            BonusAmount = input.BonusAmount,
            RetentionMonths = Math.Max(0, input.RetentionMonths),
            Status = ReferralStatuses.Submitted,
        };
        Db.HrEmployeeReferrals.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.ReferrerEmployee).LoadAsync(ct);
        return Ok(ToReferral(row, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    /// <summary>
    /// Take a referral into the pipeline, creating the candidate.
    ///
    /// Done here rather than by hand so the referral and the candidate stay
    /// joined — without that link nobody can answer who brought this person in
    /// when the bonus falls due four months later.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("referrals/{id:int}/to-candidate")]
    public async Task<ActionResult<HrReferralDto>> ReferralToCandidate(
        int id, CancellationToken ct)
    {
        var row = await Db.HrEmployeeReferrals
            .Include(r => r.ReferrerEmployee).Include(r => r.Candidate)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Referral");

        if (row.CandidateId is not null)
            throw ApiException.BadRequest("That referral is already in the pipeline.");

        var candidate = new HrCandidate
        {
            CompanyId = Db.Tenant.CompanyId,
            VacancyId = row.VacancyId,
            Name = row.CandidateName,
            Phone = row.Phone,
            Email = row.Email,
            Source = $"Referral — {row.ReferrerEmployee?.Name ?? "an employee"}",
            Stage = RecruitmentStages.Applied,
        };
        Db.HrCandidates.Add(candidate);
        await Db.SaveChangesAsync(ct);

        row.CandidateId = candidate.Id;
        row.Status = ReferralStatuses.InProcess;
        await Db.SaveChangesAsync(ct);

        return Ok(ToReferral(row, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    /// <summary>
    /// Mark a referral hired, which starts the retention clock.
    ///
    /// The bonus is not paid here. It falls due once the referred person has
    /// stayed the retention period — the only version of a referral scheme
    /// that does not become a way to churn friends through payroll.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("referrals/{id:int}/hired")]
    public async Task<ActionResult<HrReferralDto>> ReferralHired(
        int id, [FromQuery] DateOnly? on, CancellationToken ct)
    {
        var row = await Db.HrEmployeeReferrals
            .Include(r => r.ReferrerEmployee).Include(r => r.Candidate)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Referral");

        var hired = on ?? DateOnly.FromDateTime(DateTime.UtcNow);
        row.HiredOn = hired;
        row.BonusDueOn = hired.AddMonths(row.RetentionMonths);
        row.Status = ReferralStatuses.Hired;

        await Db.SaveChangesAsync(ct);
        return Ok(ToReferral(row, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    /// <summary>
    /// Pay a referral bonus that has fallen due, through the payroll.
    ///
    /// Written as an additional salary rather than settled outside payroll, so
    /// it is taxed, appears on the payslip and lands in the same bank advice as
    /// everything else.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("referrals/{id:int}/pay-bonus")]
    public async Task<ActionResult<HrReferralDto>> PayReferralBonus(
        int id, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var row = await Db.HrEmployeeReferrals
            .Include(r => r.ReferrerEmployee).Include(r => r.Candidate)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Referral");

        if (row.BonusAmount <= 0m)
            throw ApiException.BadRequest("This referral carries no bonus.");
        if (row.BonusAdditionalSalaryId is not null)
            throw ApiException.BadRequest("That bonus has already been raised.");
        if (row.HiredOn is null)
            throw ApiException.BadRequest("Nobody has been hired against this referral yet.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (row.BonusDueOn is DateOnly due && today < due)
        {
            throw ApiException.BadRequest(
                $"The bonus falls due on {due:d MMM yyyy}, once the referral has "
                + $"stayed {row.RetentionMonths} months.");
        }

        var component = await Db.HrSalaryComponents
            .FirstOrDefaultAsync(c => c.Abbreviation == "PBONUS" && c.IsActive, ct)
            ?? await Db.HrSalaryComponents.FirstOrDefaultAsync(
                c => c.ComponentType == SalaryComponentTypes.Earning
                    && !c.IsStatutory && c.IsActive, ct)
            ?? throw ApiException.BadRequest(
                "No earning component exists to pay the bonus through.");

        var additional = new HrAdditionalSalary
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = row.ReferrerEmployeeId,
            SalaryComponentId = component.Id,
            Amount = row.BonusAmount,
            Year = year,
            Month = month,
            DependsOnPaymentDays = false,
            Reason = $"Referral bonus — {row.CandidateName}",
            Status = HrRequestStatuses.Approved,
        };
        Db.HrAdditionalSalaries.Add(additional);
        await Db.SaveChangesAsync(ct);

        row.BonusAdditionalSalaryId = additional.Id;
        row.Status = ReferralStatuses.BonusPaid;
        await Db.SaveChangesAsync(ct);

        return Ok(ToReferral(row, today));
    }

    private static HrReferralDto ToReferral(HrEmployeeReferral r, DateOnly today) => new(
        r.Id, r.ReferrerEmployeeId, r.ReferrerEmployee?.Name ?? "—",
        r.CandidateName, r.Phone, r.Email, r.Position, r.Notes,
        r.CandidateId, r.VacancyId, r.Status,
        r.BonusAmount, r.RetentionMonths, r.HiredOn, r.BonusDueOn,
        r.BonusAdditionalSalaryId,
        r.BonusDueOn is DateOnly due && today >= due
            && r.BonusAdditionalSalaryId is null && r.BonusAmount > 0m);
}
