using System.Linq.Expressions;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BullEvents.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenant)
    : DbContext(options)
{
    /// <summary>
    /// Exposed so seeding and model training — which legitimately run across the
    /// whole database — can bypass the tenant filter explicitly rather than by
    /// accident.
    /// </summary>
    public TenantContext Tenant => tenant;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();

    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<SiteVisit> SiteVisits => Set<SiteVisit>();
    public DbSet<ObmVisit> ObmVisits => Set<ObmVisit>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UserModuleGrant> UserModuleGrants => Set<UserModuleGrant>();
    public DbSet<PermissionSet> PermissionSets => Set<PermissionSet>();
    public DbSet<ObjectPermission> ObjectPermissions => Set<ObjectPermission>();
    public DbSet<FieldPermission> FieldPermissions => Set<FieldPermission>();
    public DbSet<UserPermissionSet> UserPermissionSets => Set<UserPermissionSet>();
    public DbSet<ObjectVisibilityRule> ObjectVisibilityRules => Set<ObjectVisibilityRule>();
    public DbSet<SharingRule> SharingRules => Set<SharingRule>();
    public DbSet<LoginPolicy> LoginPolicies => Set<LoginPolicy>();

    /* ---------------- administration ---------------- */

    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
    public DbSet<DuplicateRule> DuplicateRules => Set<DuplicateRule>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationEvent> EscalationEvents => Set<EscalationEvent>();
    public DbSet<ApprovalProcess> ApprovalProcesses => Set<ApprovalProcess>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UsageSnapshot> UsageSnapshots => Set<UsageSnapshot>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<PickListValue> PickListValues => Set<PickListValue>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<Connector> Connectors => Set<Connector>();

    /* ---------------- organisation ---------------- */

    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamTransfer> TeamTransfers => Set<TeamTransfer>();

    /* ---------------- post sales ---------------- */

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingApplicant> BookingApplicants => Set<BookingApplicant>();
    public DbSet<BookingMilestone> BookingMilestones => Set<BookingMilestone>();
    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<BookingAgreement> BookingAgreements => Set<BookingAgreement>();
    public DbSet<HomeLoan> HomeLoans => Set<HomeLoan>();
    public DbSet<Possession> Possessions => Set<Possession>();
    public DbSet<BookingCancellation> BookingCancellations => Set<BookingCancellation>();
    public DbSet<BookingTransfer> BookingTransfers => Set<BookingTransfer>();
    public DbSet<Brokerage> Brokerages => Set<Brokerage>();
    public DbSet<BrokerageSlab> BrokerageSlabs => Set<BrokerageSlab>();
    public DbSet<BookingDocument> BookingDocuments => Set<BookingDocument>();
    public DbSet<EscrowEntry> EscrowEntries => Set<EscrowEntry>();

    /* ---------------- statutory billing ---------------- */

    public DbSet<GstProfile> GstProfiles => Set<GstProfile>();
    public DbSet<TaxInvoice> TaxInvoices => Set<TaxInvoice>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<PostDatedCheque> PostDatedCheques => Set<PostDatedCheque>();
    public DbSet<TdsCertificate> TdsCertificates => Set<TdsCertificate>();
    public DbSet<ConnectorEvent> ConnectorEvents => Set<ConnectorEvent>();

    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationFollowUp> QuotationFollowUps => Set<QuotationFollowUp>();
    public DbSet<QuotationNegotiation> QuotationNegotiations => Set<QuotationNegotiation>();
    public DbSet<QuotationActivity> QuotationActivities => Set<QuotationActivity>();
    public DbSet<QuotationShareLink> QuotationShareLinks => Set<QuotationShareLink>();
    public DbSet<QuotationTemplate> QuotationTemplates => Set<QuotationTemplate>();
    public DbSet<QuotationTemplateCharge> QuotationTemplateCharges => Set<QuotationTemplateCharge>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Tower> Towers => Set<Tower>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitStatusHistory> UnitStatusHistories => Set<UnitStatusHistory>();

    /// <summary>Date-and-slot availability for every bookable space.</summary>
    public DbSet<SpaceBooking> SpaceBookings => Set<SpaceBooking>();

    public DbSet<VenuePackage> VenuePackages => Set<VenuePackage>();
    public DbSet<VenuePackageSpace> VenuePackageSpaces => Set<VenuePackageSpace>();
    public DbSet<VenuePeakDate> VenuePeakDates => Set<VenuePeakDate>();

    /* ---------------- rentable asset inventory (props, décor, stock) ---------------- */

    public DbSet<PropStore> PropStores => Set<PropStore>();
    public DbSet<PropCategory> PropCategories => Set<PropCategory>();
    public DbSet<PropItem> PropItems => Set<PropItem>();
    public DbSet<PropItemPhoto> PropItemPhotos => Set<PropItemPhoto>();

    /// <summary>Append-only stock ledger. The condition counts are its running total.</summary>
    public DbSet<PropStockMovement> PropStockMovements => Set<PropStockMovement>();

    public DbSet<PropIssue> PropIssues => Set<PropIssue>();
    public DbSet<PropIssueLine> PropIssueLines => Set<PropIssueLine>();

    /// <summary>Sets that travel together — a mandap, an entrance arch.</summary>
    public DbSet<PropKit> PropKits => Set<PropKit>();
    public DbSet<PropKitLine> PropKitLines => Set<PropKitLine>();

    /// <summary>Quantity-over-a-window holds. The props answer to SpaceBooking.</summary>
    public DbSet<PropReservation> PropReservations => Set<PropReservation>();

    /* ---------------- vendors, crew and fleet ---------------- */

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorRate> VendorRates => Set<VendorRate>();
    public DbSet<VendorDocument> VendorDocuments => Set<VendorDocument>();
    public DbSet<VendorPurchaseOrder> VendorPurchaseOrders => Set<VendorPurchaseOrder>();
    public DbSet<VendorPoLine> VendorPoLines => Set<VendorPoLine>();
    public DbSet<VendorPayment> VendorPayments => Set<VendorPayment>();

    public DbSet<CrewMember> CrewMembers => Set<CrewMember>();

    /// <summary>Person-over-a-window holds. The crew answer to PropReservation.</summary>
    public DbSet<CrewAssignment> CrewAssignments => Set<CrewAssignment>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleTrip> VehicleTrips => Set<VehicleTrip>();
    public DbSet<VehicleTripLoad> VehicleTripLoads => Set<VehicleTripLoad>();

    /// <summary>What it takes to deliver a proposal — and what that costs.</summary>
    public DbSet<QuotationResource> QuotationResources => Set<QuotationResource>();

    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<ChargeHead> ChargeHeads => Set<ChargeHead>();
    public DbSet<QuotationCharge> QuotationCharges => Set<QuotationCharge>();
    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();
    public DbSet<PaymentPlanMilestone> PaymentPlanMilestones => Set<PaymentPlanMilestone>();
    public DbSet<QuotationMilestone> QuotationMilestones => Set<QuotationMilestone>();
    public DbSet<Approval> Approvals => Set<Approval>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<HrDepartment> HrDepartments => Set<HrDepartment>();
    public DbSet<HrEmployee> HrEmployees => Set<HrEmployee>();
    public DbSet<HrEmployeeDocument> HrEmployeeDocuments => Set<HrEmployeeDocument>();
    public DbSet<HrShift> HrShifts => Set<HrShift>();
    public DbSet<HrAttendance> HrAttendances => Set<HrAttendance>();
    public DbSet<HrAttendanceCorrection> HrAttendanceCorrections => Set<HrAttendanceCorrection>();
    public DbSet<HrLeaveType> HrLeaveTypes => Set<HrLeaveType>();
    public DbSet<HrLeaveBalance> HrLeaveBalances => Set<HrLeaveBalance>();

    /* ---------------- leave, allocated rather than assumed ---------------- */

    public DbSet<HrLeavePeriod> HrLeavePeriods => Set<HrLeavePeriod>();
    public DbSet<HrLeavePolicy> HrLeavePolicies => Set<HrLeavePolicy>();
    public DbSet<HrLeavePolicyLine> HrLeavePolicyLines => Set<HrLeavePolicyLine>();
    public DbSet<HrLeavePolicyAssignment> HrLeavePolicyAssignments =>
        Set<HrLeavePolicyAssignment>();
    public DbSet<HrLeaveAllocation> HrLeaveAllocations => Set<HrLeaveAllocation>();
    public DbSet<HrCompensatoryRequest> HrCompensatoryRequests => Set<HrCompensatoryRequest>();
    public DbSet<HrLeaveEncashment> HrLeaveEncashments => Set<HrLeaveEncashment>();
    public DbSet<HrLeaveBlockDate> HrLeaveBlockDates => Set<HrLeaveBlockDate>();

    /* ---------------- shifts and attendance requests ---------------- */

    public DbSet<HrShiftAssignment> HrShiftAssignments => Set<HrShiftAssignment>();
    public DbSet<HrShiftRequest> HrShiftRequests => Set<HrShiftRequest>();
    public DbSet<HrAttendanceRequest> HrAttendanceRequests => Set<HrAttendanceRequest>();

    /* ---------------- pay, made of components ---------------- */

    public DbSet<HrSalaryComponent> HrSalaryComponents => Set<HrSalaryComponent>();
    public DbSet<HrPayStructure> HrPayStructures => Set<HrPayStructure>();
    public DbSet<HrPayStructureLine> HrPayStructureLines => Set<HrPayStructureLine>();
    public DbSet<HrPayStructureAssignment> HrPayStructureAssignments =>
        Set<HrPayStructureAssignment>();
    public DbSet<HrAdditionalSalary> HrAdditionalSalaries => Set<HrAdditionalSalary>();
    public DbSet<HrEmployeeAdvance> HrEmployeeAdvances => Set<HrEmployeeAdvance>();
    public DbSet<HrAdvanceRepayment> HrAdvanceRepayments => Set<HrAdvanceRepayment>();
    public DbSet<HrPayslipLine> HrPayslipLines => Set<HrPayslipLine>();

    /* ---------------- talent ---------------- */

    public DbSet<HrStaffingPlan> HrStaffingPlans => Set<HrStaffingPlan>();
    public DbSet<HrStaffingPlanLine> HrStaffingPlanLines => Set<HrStaffingPlanLine>();
    public DbSet<HrJobRequisition> HrJobRequisitions => Set<HrJobRequisition>();
    public DbSet<HrInterviewRound> HrInterviewRounds => Set<HrInterviewRound>();
    public DbSet<HrInterviewSkill> HrInterviewSkills => Set<HrInterviewSkill>();
    public DbSet<HrInterviewFeedback> HrInterviewFeedbacks => Set<HrInterviewFeedback>();
    public DbSet<HrInterviewSkillRating> HrInterviewSkillRatings =>
        Set<HrInterviewSkillRating>();
    public DbSet<HrJobOffer> HrJobOffers => Set<HrJobOffer>();
    public DbSet<HrEmployeeReferral> HrEmployeeReferrals => Set<HrEmployeeReferral>();

    public DbSet<HrAppraisalTemplate> HrAppraisalTemplates => Set<HrAppraisalTemplate>();
    public DbSet<HrAppraisalTemplateKra> HrAppraisalTemplateKras =>
        Set<HrAppraisalTemplateKra>();
    public DbSet<HrAppraisalKra> HrAppraisalKras => Set<HrAppraisalKra>();
    public DbSet<HrPerformanceFeedback> HrPerformanceFeedbacks =>
        Set<HrPerformanceFeedback>();

    public DbSet<HrTrainingFeedback> HrTrainingFeedbacks => Set<HrTrainingFeedback>();

    /* ---------------- the workplace ---------------- */

    public DbSet<HrChecklistTemplate> HrChecklistTemplates => Set<HrChecklistTemplate>();
    public DbSet<HrChecklistTask> HrChecklistTasks => Set<HrChecklistTask>();
    public DbSet<HrGrievance> HrGrievances => Set<HrGrievance>();
    public DbSet<HrSkill> HrSkills => Set<HrSkill>();
    public DbSet<HrEmployeeSkill> HrEmployeeSkills => Set<HrEmployeeSkill>();
    public DbSet<HrExpenseClaimType> HrExpenseClaimTypes => Set<HrExpenseClaimType>();
    public DbSet<HrTravelRequest> HrTravelRequests => Set<HrTravelRequest>();
    public DbSet<HrTravelLeg> HrTravelLegs => Set<HrTravelLeg>();
    public DbSet<HrTimesheet> HrTimesheets => Set<HrTimesheet>();
    public DbSet<HrTimesheetLine> HrTimesheetLines => Set<HrTimesheetLine>();
    public DbSet<HrLeaveRequest> HrLeaveRequests => Set<HrLeaveRequest>();
    public DbSet<HrSalaryStructure> HrSalaryStructures => Set<HrSalaryStructure>();
    public DbSet<HrSalaryRevision> HrSalaryRevisions => Set<HrSalaryRevision>();
    public DbSet<HrPayrollRun> HrPayrollRuns => Set<HrPayrollRun>();
    public DbSet<HrPayslip> HrPayslips => Set<HrPayslip>();
    public DbSet<HrVacancy> HrVacancies => Set<HrVacancy>();
    public DbSet<HrCandidate> HrCandidates => Set<HrCandidate>();
    public DbSet<HrOnboardingItem> HrOnboardingItems => Set<HrOnboardingItem>();
    public DbSet<HrLetter> HrLetters => Set<HrLetter>();
    public DbSet<HrResignation> HrResignations => Set<HrResignation>();
    public DbSet<HrFullAndFinal> HrFullAndFinals => Set<HrFullAndFinal>();
    public DbSet<HrEventDeployment> HrEventDeployments => Set<HrEventDeployment>();
    public DbSet<HrAssetIssue> HrAssetIssues => Set<HrAssetIssue>();
    public DbSet<HrHoliday> HrHolidays => Set<HrHoliday>();
    public DbSet<HrPolicy> HrPolicies => Set<HrPolicy>();
    public DbSet<HrAnnouncement> HrAnnouncements => Set<HrAnnouncement>();
    public DbSet<HrPunch> HrPunches => Set<HrPunch>();
    public DbSet<HrExpenseClaim> HrExpenseClaims => Set<HrExpenseClaim>();
    public DbSet<HrHelpdeskTicket> HrHelpdeskTickets => Set<HrHelpdeskTicket>();
    public DbSet<HrAppraisalCycle> HrAppraisalCycles => Set<HrAppraisalCycle>();
    public DbSet<HrGoal> HrGoals => Set<HrGoal>();
    public DbSet<HrAppraisal> HrAppraisals => Set<HrAppraisal>();
    public DbSet<HrInterview> HrInterviews => Set<HrInterview>();
    public DbSet<HrTraining> HrTrainings => Set<HrTraining>();
    public DbSet<HrTrainingEnrolment> HrTrainingEnrolments => Set<HrTrainingEnrolment>();
    public DbSet<HrLifecycleEvent> HrLifecycleEvents => Set<HrLifecycleEvent>();
    public DbSet<HrSiteAllocation> HrSiteAllocations => Set<HrSiteAllocation>();
    public DbSet<HrSalesKpi> HrSalesKpis => Set<HrSalesKpi>();
    public DbSet<HrTerritoryMap> HrTerritoryMaps => Set<HrTerritoryMap>();
    public DbSet<HrFieldVisit> HrFieldVisits => Set<HrFieldVisit>();
    public DbSet<HrPolicyAck> HrPolicyAcks => Set<HrPolicyAck>();

    /* ---------------- India statutory payroll ---------------- */

    /// <summary>Effective-dated PF, ESI, LWF and gratuity rates.</summary>
    public DbSet<HrStatutoryConfig> HrStatutoryConfigs => Set<HrStatutoryConfig>();
    public DbSet<HrProfessionalTaxSlab> HrProfessionalTaxSlabs => Set<HrProfessionalTaxSlab>();
    public DbSet<HrIncomeTaxSlab> HrIncomeTaxSlabs => Set<HrIncomeTaxSlab>();
    public DbSet<HrTaxRegimeConfig> HrTaxRegimeConfigs => Set<HrTaxRegimeConfig>();
    public DbSet<HrEmployeeTaxProfile> HrEmployeeTaxProfiles => Set<HrEmployeeTaxProfile>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// The collation every searchable text column uses.
    ///
    /// MySQL folded case by default and the whole search surface — the query
    /// engine's contains/startsWith, the free-text box on every list view —
    /// was written against that. Postgres compares text exactly, so the fold
    /// has to be asked for. Declaring it once and applying it to the string
    /// columns keeps the behaviour identical without rewriting a single query:
    /// nothing errors when it is missing, results just quietly thin out, which
    /// is the worst way for a regression to arrive.
    /// </summary>
    public const string CaseInsensitiveCollation = "crm_ci";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ICU, non-deterministic, strength 2: "Rajesh" == "rajesh", and the
        // accents still count.
        modelBuilder.HasCollation(
            CaseInsensitiveCollation,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false);

        ConfigureFoundation(modelBuilder);
        ConfigureLeads(modelBuilder);
        ConfigureCrm(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureProps(modelBuilder);
        ConfigureResources(modelBuilder);
        ConfigureHr(modelBuilder);
        ConfigureAudit(modelBuilder);
        ConfigureAccess(modelBuilder);
        ConfigurePostSales(modelBuilder);

        ApplyTenantFilters(modelBuilder);
        ApplyMoneyPrecision(modelBuilder);
        ApplyRatePrecision(modelBuilder);
        ApplySearchCollation(modelBuilder);
        ApplyTimestampMapping(modelBuilder);
    }

    /// <summary>
    /// Stores every timestamp with a zone, and normalises every value to UTC.
    ///
    /// The zoneless alternative looked closer to what MySQL did, but it breaks
    /// the moment a query says <c>x.ValidUntil &lt; DateTime.UtcNow</c>: Npgsql
    /// renders that as <c>now()</c>, which is <c>timestamptz</c>, and Postgres
    /// refuses to compare the two. Fixing that at each call site means finding
    /// every one and trusting whoever writes the next; fixing it here means the
    /// comparison simply works.
    ///
    /// The converter is what makes that safe. Npgsql rejects any value whose
    /// Kind is not Utc, and this system produces two sorts: writes are
    /// <see cref="DateTime.UtcNow"/>, while the reporting code builds period
    /// boundaries with <c>new DateTime(year, 1, 1)</c>, which is Unspecified by
    /// construction. Unspecified is taken as already-UTC — it always was, MySQL
    /// included — and Local is converted properly rather than relabelled.
    /// </summary>
    private static void ApplyTimestampMapping(ModelBuilder modelBuilder)
    {
        var toUtc = new ValueConverter<DateTime, DateTime>(
            write => write.Kind == DateTimeKind.Local
                ? write.ToUniversalTime()
                : DateTime.SpecifyKind(write, DateTimeKind.Utc),
            read => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        var toUtcNullable = new ValueConverter<DateTime?, DateTime?>(
            write => write == null
                ? null
                : write.Value.Kind == DateTimeKind.Local
                    ? write.Value.ToUniversalTime()
                    : DateTime.SpecifyKind(write.Value, DateTimeKind.Utc),
            read => read == null
                ? null
                : DateTime.SpecifyKind(read.Value, DateTimeKind.Utc));

        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("timestamp with time zone");
            property.SetValueConverter(
                property.ClrType == typeof(DateTime) ? toUtc : toUtcNullable);
        }
    }

    /// <summary>
    /// Folds case on the text columns the app searches.
    ///
    /// Applied to every string property rather than to a hand-picked list: the
    /// query engine builds filters from field metadata, so any column can end
    /// up behind a "contains", and a column that was missed would be the one
    /// nobody notices until a customer says their name cannot be found.
    ///
    /// Keys and tokens are the exception — a share-link token and a quote
    /// number are identifiers, and folding case on an identifier makes two
    /// different values collide.
    /// </summary>
    private static void ApplySearchCollation(ModelBuilder modelBuilder)
    {
        string[] exact = ["Token", "PasswordHash", "Slug"];

        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string))
            .Where(p => !exact.Contains(p.Name))
            // A collation is a property of text. The audit trail's change set is
            // stored as jsonb so it can be indexed and queried, and Postgres
            // rejects the column outright if one is applied to it.
            .Where(p => p.GetColumnType()?.Contains("json", StringComparison.OrdinalIgnoreCase) != true))
        {
            property.SetCollation(CaseInsensitiveCollation);
        }
    }

    /* ------------------------------------------------------------------ *
     * Tenant isolation
     *
     * Every ITenantScoped entity gets the same compiled filter: rows belong to
     * the caller's company, and soft-deleted rows are invisible. The tenant id
     * is read through the TenantContext instance rather than captured, so one
     * compiled model serves every request.
     * ------------------------------------------------------------------ */
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable) continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? predicate = null;

            if (isTenantScoped)
            {
                // `!tenant.IsResolved || e.CompanyId == tenant.CompanyId`
                var contextConstant = Expression.Constant(this);
                var tenantProperty = Expression.Property(contextConstant, nameof(Tenant));

                var resolved = Expression.Property(tenantProperty, nameof(TenantContext.IsResolved));
                var companyId = Expression.Property(tenantProperty, nameof(TenantContext.CompanyId));
                var rowCompanyId = Expression.Property(parameter, nameof(ITenantScoped.CompanyId));

                predicate = Expression.OrElse(
                    Expression.Not(resolved),
                    Expression.Equal(rowCompanyId, companyId));
            }

            if (isSoftDeletable)
            {
                var notDeleted = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
                predicate = predicate is null
                    ? notDeleted
                    : Expression.AndAlso(predicate, notDeleted);
            }

            modelBuilder.Entity(clrType)
                .HasQueryFilter(Expression.Lambda(predicate!, parameter));
        }
    }

    /// <summary>MySQL defaults decimals to (18,2); money columns say so explicitly.</summary>
    private static void ApplyMoneyPrecision(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }

    /// <summary>
    /// The columns holding fractions rather than money, widened to six decimals.
    ///
    /// Money is carried at two decimals and a rate is not money. A discount of
    /// 12.5% is stored as 0.125, and at scale two that column rounds it to 0.13
    /// — the quotation then re-prices itself at thirteen percent the next time
    /// anyone edits it, and prints a discount the desk never granted. The same
    /// applies to a nineteen-instalment schedule, where each milestone's share
    /// is a number like 0.0714.
    ///
    /// Listed explicitly rather than matched on a name: "TaxAmount" and
    /// "TaxRate" differ by one word, and getting a money column into this set
    /// would silently cap every price in the system at four figures.
    /// </summary>
    private static void ApplyRatePrecision(ModelBuilder modelBuilder)
    {
        var fractions = new (Type Entity, string[] Properties)[]
        {
            (typeof(Quotation), [
                nameof(Quotation.DiscountPercent),
                nameof(Quotation.StandardDiscountPercent),
                nameof(Quotation.TaxPercent),
                nameof(Quotation.AssuredReturnPercent),
                nameof(Quotation.AssuredReturnYears),
                nameof(Quotation.BuyBackPercentPerYear),
                nameof(Quotation.BuyBackEligibleAfterYears),
                nameof(Quotation.BuyBackHorizonYears),
                nameof(Quotation.RentPerSqftPerMonth),
                nameof(Quotation.GrossRentalYield),
                nameof(Quotation.ReturnOnInvestment),
                nameof(Quotation.ReturnHorizonYears),
            ]),
            (typeof(QuotationMilestone), [nameof(QuotationMilestone.Percent)]),
            (typeof(QuotationCharge), [nameof(QuotationCharge.TaxRate)]),
            (typeof(QuotationLine), [nameof(QuotationLine.DiscountPercent)]),
            (typeof(QuotationTemplate), [nameof(QuotationTemplate.DefaultDiscount)]),
            (typeof(PaymentPlan), [
                nameof(PaymentPlan.StandardDiscount),
                nameof(PaymentPlan.DiscountTolerance),
                nameof(PaymentPlan.TaxRate),
                nameof(PaymentPlan.AssuredReturnPercent),
                nameof(PaymentPlan.AssuredReturnYears),
                nameof(PaymentPlan.BuyBackPercentPerYear),
                nameof(PaymentPlan.BuyBackEligibleAfterYears),
            ]),
            (typeof(PaymentPlanMilestone), [nameof(PaymentPlanMilestone.Percent)]),

            // The booking's copy of the schedule, for the same reason as the
            // quotation's. Left at money precision it rounded each instalment
            // share to whole percentage points on the way in — a 20% slab
            // stored as 0.19 — and the schedule then printed a split back to
            // the customer that was not the one they signed.
            (typeof(BookingMilestone), [nameof(BookingMilestone.Percent)]),

            (typeof(ChargeHead), [nameof(ChargeHead.TaxRate)]),

            // The statutory rates, for exactly the reason above and with
            // sharper teeth: the pension contribution is 8.33%, and at money
            // precision that column stores 0.08. Every payslip then sends the
            // wrong figure to EPS and the ECR return disagrees with the
            // challan. ESI's 0.75% rounds to 0.01 — a third too much taken from
            // an employee who is by definition among the lowest paid.
            (typeof(HrStatutoryConfig), [
                nameof(HrStatutoryConfig.PfEmployeeRate),
                nameof(HrStatutoryConfig.PfEmployerRate),
                nameof(HrStatutoryConfig.EpsRate),
                nameof(HrStatutoryConfig.EdliRate),
                nameof(HrStatutoryConfig.PfAdminRate),
                nameof(HrStatutoryConfig.EsiEmployeeRate),
                nameof(HrStatutoryConfig.EsiEmployerRate),
                nameof(HrStatutoryConfig.CessRate),
            ]),

            (typeof(HrIncomeTaxSlab), [nameof(HrIncomeTaxSlab.Rate)]),
        };

        foreach (var (entity, properties) in fractions)
        {
            var type = modelBuilder.Model.FindEntityType(entity);
            if (type is null) continue;

            foreach (var name in properties)
            {
                type.FindProperty(name)?.SetPrecision(9);
                type.FindProperty(name)?.SetScale(6);
            }
        }
    }

    /// <summary>
    /// The reporting line and the per-user module exceptions.
    /// </summary>
    private static void ConfigureAccess(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // Restrict, not Cascade: deleting a manager must not delete their
            // whole team. The reporting line is reassigned by a human.
            entity.HasOne(u => u.Manager)
                .WithMany(u => u.Reports)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(u => u.ManagerId);
        });

        modelBuilder.Entity<PermissionSet>(entity =>
        {
            entity.HasIndex(p => new { p.CompanyId, p.Name }).IsUnique();

            // One profile per role per company — two would make "the baseline"
            // ambiguous, and the resolver would silently pick whichever came back
            // first.
            entity.HasIndex(p => new { p.CompanyId, p.RoleKey })
                .IsUnique()
                .HasFilter("\"IsProfile\" = true");
        });

        modelBuilder.Entity<ObjectPermission>(entity =>
        {
            entity.HasOne(o => o.PermissionSet)
                .WithMany(p => p.ObjectPermissions)
                .HasForeignKey(o => o.PermissionSetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => new { o.PermissionSetId, o.Object }).IsUnique();
        });

        modelBuilder.Entity<FieldPermission>(entity =>
        {
            entity.HasOne(f => f.PermissionSet)
                .WithMany(p => p.FieldPermissions)
                .HasForeignKey(f => f.PermissionSetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => new { f.PermissionSetId, f.Object, f.Field }).IsUnique();
        });

        modelBuilder.Entity<UserPermissionSet>(entity =>
        {
            entity.HasOne(a => a.User).WithMany()
                .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.PermissionSet).WithMany(p => p.Assignments)
                .HasForeignKey(a => a.PermissionSetId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.UserId, a.PermissionSetId }).IsUnique();
        });

        modelBuilder.Entity<ObjectVisibilityRule>(entity =>
            entity.HasIndex(v => new { v.CompanyId, v.Object }).IsUnique());

        modelBuilder.Entity<SharingRule>(entity =>
            entity.HasIndex(r => new { r.CompanyId, r.Object, r.IsActive }));

        /* ---------------- administration ---------------- */

        // One default per kind. A bulk run that has to choose between three
        // demand-letter drafts chooses silently, and silently is the problem.
        modelBuilder.Entity<DocumentTemplate>(entity =>
            entity.HasIndex(t => new { t.CompanyId, t.Kind, t.IsDefault }));

        modelBuilder.Entity<GeneratedDocument>(entity =>
        {
            entity.HasOne(g => g.Booking).WithMany()
                .HasForeignKey(g => g.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.Demand).WithMany()
                .HasForeignKey(g => g.DemandId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(g => new { g.BookingId, g.Kind });
            entity.HasIndex(g => new { g.CompanyId, g.Number }).IsUnique();
        });

        modelBuilder.Entity<BusinessHours>(entity =>
        {
            entity.HasMany(b => b.Holidays).WithOne(h => h.BusinessHours)
                .HasForeignKey(h => h.BusinessHoursId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.CompanyId, b.IsDefault });
        });

        modelBuilder.Entity<Holiday>(entity =>
            entity.HasIndex(h => new { h.BusinessHoursId, h.Date }));

        // Ordered by SortOrder on every read — first match wins, so the index
        // carries the order rather than leaving it to a sort at query time.
        modelBuilder.Entity<AssignmentRule>(entity =>
            entity.HasIndex(r => new { r.CompanyId, r.Object, r.IsActive, r.SortOrder }));

        modelBuilder.Entity<DuplicateRule>(entity =>
            entity.HasIndex(r => new { r.CompanyId, r.Object, r.IsActive }));

        modelBuilder.Entity<EscalationRule>(entity =>
        {
            entity.HasOne(r => r.BusinessHours).WithMany()
                .HasForeignKey(r => r.BusinessHoursId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => new { r.CompanyId, r.Object, r.IsActive });
        });

        // The unique index is what makes an escalation fire once: the sweep
        // inserts before it acts, and a second attempt on the same record and
        // rule collides instead of alerting again.
        modelBuilder.Entity<EscalationEvent>(entity =>
        {
            entity.HasOne(e => e.EscalationRule).WithMany()
                .HasForeignKey(e => e.EscalationRuleId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.EscalationRuleId, e.Object, e.RecordId }).IsUnique();
        });

        modelBuilder.Entity<ApprovalProcess>(entity =>
        {
            entity.HasMany(p => p.Steps).WithOne(s => s.ApprovalProcess)
                .HasForeignKey(s => s.ApprovalProcessId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.CompanyId, p.Object, p.IsActive });
        });

        modelBuilder.Entity<ApprovalStep>(entity =>
            entity.HasIndex(s => new { s.ApprovalProcessId, s.SortOrder }));

        modelBuilder.Entity<ScheduledJob>(entity =>
            entity.HasIndex(j => new { j.CompanyId, j.IsActive, j.NextRunAt }));

        modelBuilder.Entity<LoginPolicy>(entity =>
        {
            entity.HasOne(l => l.PermissionSet).WithMany()
                .HasForeignKey(l => l.PermissionSetId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => l.PermissionSetId).IsUnique();
        });

        modelBuilder.Entity<UserModuleGrant>(entity =>
        {
            entity.HasOne(g => g.User)
                .WithMany(u => u.ModuleGrants)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One decision per module per person — a user cannot be both
            // granted and denied the same thing.
            entity.HasIndex(g => new { g.UserId, g.Module }).IsUnique();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // The token carries this and nothing else about the row, so the
            // lookup on it happens on every authenticated request.
            entity.HasIndex(s => s.TokenId).IsUnique();

            // Backs the sessions list and the bulk revoke.
            entity.HasIndex(s => new { s.UserId, s.RevokedAt });

            entity.Property(s => s.RevokedReason).HasMaxLength(64);
            entity.Property(s => s.Device).HasMaxLength(200);
            entity.Property(s => s.IpAddress).HasMaxLength(64);
        });

        // The custom-field bags. jsonb rather than text so Postgres can look
        // inside them — and so the case-folding collation pass skips them, since
        // a non-deterministic collation makes a column reject LIKE outright.
        modelBuilder.Entity<Lead>()
            .Property(l => l.CustomFields).HasColumnType("jsonb");
        modelBuilder.Entity<Contact>()
            .Property(c => c.CustomFields).HasColumnType("jsonb");
        modelBuilder.Entity<Opportunity>()
            .Property(o => o.CustomFields).HasColumnType("jsonb");

        modelBuilder.Entity<CustomFieldDefinition>(entity =>
        {
            // One key per object per company. Two fields sharing a key would
            // write to the same JSON property and the second would silently win.
            entity.HasIndex(f => new { f.CompanyId, f.Object, f.Key }).IsUnique();

            entity.Property(f => f.Object).HasMaxLength(64);
            entity.Property(f => f.Key).HasMaxLength(64);
            entity.Property(f => f.Label).HasMaxLength(120);
        });

        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.CompanyId, l.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        modelBuilder.Entity<Connector>(entity =>
        {
            // The token is the credential a portal holds, and it is looked up on
            // every inbound delivery.
            entity.HasIndex(c => c.InboundToken).IsUnique();
            entity.HasIndex(c => new { c.CompanyId, c.Provider });

            entity.HasOne(c => c.DefaultBranch)
                .WithMany()
                .HasForeignKey(c => c.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.DefaultOwner)
                .WithMany()
                .HasForeignKey(c => c.DefaultOwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(c => c.Provider).HasMaxLength(48);
            entity.Property(c => c.Name).HasMaxLength(120);
            entity.Property(c => c.Status).HasMaxLength(24);
            entity.Property(c => c.SourceLabel).HasMaxLength(80);
            entity.Property(c => c.LastError).HasMaxLength(300);

            // Credentials and settings are jsonb: queryable if it ever matters,
            // and out of the case-folding collation pass either way.
            entity.Property(c => c.CredentialsJson).HasColumnType("jsonb");
            entity.Property(c => c.SettingsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ConnectorEvent>(entity =>
        {
            entity.HasOne(e => e.Connector)
                .WithMany()
                .HasForeignKey(e => e.ConnectorId)
                .OnDelete(DeleteBehavior.Cascade);

            // The activity log's own query: one connector, newest first.
            entity.HasIndex(e => new { e.ConnectorId, e.At });

            entity.Property(e => e.Kind).HasMaxLength(24);
            entity.Property(e => e.Outcome).HasMaxLength(24);
            entity.Property(e => e.Detail).HasMaxLength(300);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            // The prefix is what a presented key is looked up by, on every
            // public-API request.
            entity.HasIndex(k => k.Prefix);

            entity.Property(k => k.Name).HasMaxLength(120);
            entity.Property(k => k.Prefix).HasMaxLength(32);
            entity.Property(k => k.SecretHash).HasMaxLength(128);
            entity.Property(k => k.LastUsedIp).HasMaxLength(64);
        });

        modelBuilder.Entity<WebhookEndpoint>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Url).HasMaxLength(500);
            entity.Property(e => e.Secret).HasMaxLength(128);
            entity.Property(e => e.DisabledReason).HasMaxLength(300);
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasOne(d => d.Endpoint)
                .WithMany()
                .HasForeignKey(d => d.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);

            // The sender's own query: pending work whose time has come.
            entity.HasIndex(d => new { d.Status, d.NextAttemptAt });

            entity.Property(d => d.Event).HasMaxLength(64);
            entity.Property(d => d.Error).HasMaxLength(300);

            // The payload is JSON and can be large; jsonb keeps it queryable and
            // out of the case-folding collation pass.
            entity.Property(d => d.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<PickListValue>(entity =>
        {
            entity.HasIndex(v => new { v.CompanyId, v.List, v.Value }).IsUnique();

            entity.Property(v => v.List).HasMaxLength(48);
            entity.Property(v => v.Value).HasMaxLength(80);
            entity.Property(v => v.Label).HasMaxLength(120);
        });

        modelBuilder.Entity<UsageSnapshot>(entity =>
        {
            // One row per tenant per day. The unique index is what makes the
            // recorder idempotent — a restart on the same day updates the row it
            // already wrote instead of adding a second one.
            entity.HasIndex(u => new { u.CompanyId, u.Day }).IsUnique();

            entity.Property(u => u.PlanTier).HasMaxLength(32);
        });
    }

    /* ------------------------------------------------------------------ *
     * Post sales
     *
     * The money side of a sold unit. Two shapes recur: a child collection that
     * dies with its booking (cascade), and a lookup that must survive the row it
     * points at (restrict or set null) — a user leaving cannot take a payout
     * history with them.
     * ------------------------------------------------------------------ */
    private static void ConfigurePostSales(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            // A booking number is quoted on the phone and printed on the
            // allotment letter. Two of them is a support call nobody can resolve.
            entity.HasIndex(b => new { b.CompanyId, b.BookingNumber }).IsUnique();

            // The rule the whole module rests on: one live booking per unit.
            // Enforced here rather than by the service alone, because two reps
            // closing the same flat a second apart is a real morning in this
            // business and only the database can actually refuse the second one.
            entity.HasIndex(b => b.UnitId)
                .IsUnique()
                .HasFilter("\"Status\" <> 'Cancelled' AND \"IsDeleted\" = false");

            // What the collections desk filters by, every time it opens.
            entity.HasIndex(b => new { b.CompanyId, b.Status, b.OverdueDays });

            entity.HasOne(b => b.Unit).WithMany().HasForeignKey(b => b.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(b => b.BookingNumber).HasMaxLength(48);
            entity.Property(b => b.Status).HasMaxLength(32);
            entity.Property(b => b.ProjectName).HasMaxLength(160);
            entity.Property(b => b.TowerName).HasMaxLength(120);
            entity.Property(b => b.UnitNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<BookingApplicant>(entity =>
        {
            entity.HasOne(a => a.Booking).WithMany(b => b.Applicants)
                .HasForeignKey(a => a.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(a => a.Role).HasMaxLength(24);
            entity.Property(a => a.Name).HasMaxLength(160);
            entity.Property(a => a.Pan).HasMaxLength(16);
            entity.Property(a => a.AadhaarLast4).HasMaxLength(4);
            entity.Property(a => a.KycStatus).HasMaxLength(16);
        });

        modelBuilder.Entity<BookingMilestone>(entity =>
        {
            entity.HasOne(m => m.Booking).WithMany(b => b.Milestones)
                .HasForeignKey(m => m.BookingId).OnDelete(DeleteBehavior.Cascade);

            // The bulk demand run's own query: every pending instalment on a
            // construction stage, across a tower.
            entity.HasIndex(m => new { m.CompanyId, m.ConstructionStage, m.Status });

            entity.Property(m => m.Label).HasMaxLength(160);
            entity.Property(m => m.Status).HasMaxLength(16);
            entity.Property(m => m.ConstructionStage).HasMaxLength(64);
        });

        /* ---------------- organisation ---------------- */

        modelBuilder.Entity<Company>(entity =>
        {
            // Composed from the parts on read. A stored copy goes stale the
            // first time somebody edits the city, and then the letterhead and
            // the address form disagree.
            entity.Ignore(c => c.PostalAddress);

            entity.Property(c => c.LegalName).HasMaxLength(200);
            entity.Property(c => c.Cin).HasMaxLength(21);
            entity.Property(c => c.Pan).HasMaxLength(10);
            entity.Property(c => c.Tan).HasMaxLength(10);
            entity.Property(c => c.ReraNumber).HasMaxLength(64);
            entity.Property(c => c.AddressLine1).HasMaxLength(200);
            entity.Property(c => c.AddressLine2).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(80);
            entity.Property(c => c.State).HasMaxLength(80);
            entity.Property(c => c.Pincode).HasMaxLength(10);
            entity.Property(c => c.Phone).HasMaxLength(32);
            entity.Property(c => c.Email).HasMaxLength(160);
            entity.Property(c => c.SupportEmail).HasMaxLength(160);
            entity.Property(c => c.Website).HasMaxLength(200);
            // Defaults declared on the column, not only on the C# property.
            // A property initialiser runs when *this* code constructs the
            // object; the rows that already existed when the column was added
            // got the CLR default instead — a financial year starting in month
            // zero, and a blank currency.
            entity.Property(c => c.CurrencyCode).HasMaxLength(3).HasDefaultValue("INR");

            entity.Property(c => c.TimeZoneId)
                .HasMaxLength(64)
                .HasDefaultValue("India Standard Time");

            entity.Property(c => c.FinancialYearStartMonth).HasDefaultValue(4);
            entity.Property(c => c.Country).HasMaxLength(80).HasDefaultValue("India");
        });

        modelBuilder.Entity<Designation>(entity =>
        {
            entity.HasIndex(d => new { d.CompanyId, d.Name }).IsUnique();
            entity.HasIndex(d => new { d.CompanyId, d.Level });

            entity.Property(d => d.Name).HasMaxLength(120);
            entity.Property(d => d.Code).HasMaxLength(24);
            entity.Property(d => d.SuggestedRole).HasMaxLength(32);
        });

        modelBuilder.Entity<User>(entity =>
        {
            // Restrict, not cascade: deleting a title must never take the
            // people who held it with it. The controller refuses to retire one
            // with holders, and this is the backstop under that.
            entity.HasOne(u => u.Designation).WithMany()
                .HasForeignKey(u => u.DesignationId).OnDelete(DeleteBehavior.Restrict);

            entity.Property(u => u.EmployeeCode).HasMaxLength(32);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasOne(t => t.Branch).WithMany()
                .HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.LeadUser).WithMany()
                .HasForeignKey(t => t.LeadUserId).OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.ParentTeam).WithMany()
                .HasForeignKey(t => t.ParentTeamId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.CompanyId, t.Name });
            entity.HasIndex(t => new { t.CompanyId, t.Kind, t.IsActive });

            entity.Property(t => t.Name).HasMaxLength(120);
            entity.Property(t => t.Code).HasMaxLength(24);
            entity.Property(t => t.Kind).HasMaxLength(32);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasOne(m => m.Team).WithMany(t => t.Members)
                .HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User).WithMany(u => u.TeamMemberships)
                .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

            // Only one *live* membership per person per team. A filtered index
            // rather than a plain unique one, because the whole point is that
            // closed memberships stay — somebody who left a desk and came back
            // is two rows, and a plain unique key would refuse the return.
            entity.HasIndex(m => new { m.TeamId, m.UserId })
                .IsUnique()
                .HasFilter("\"LeftOn\" IS NULL");

            entity.HasIndex(m => new { m.CompanyId, m.UserId, m.LeftOn });

            entity.Property(m => m.RoleInTeam).HasMaxLength(16);

            entity.Ignore(m => m.IsLive);
        });

        modelBuilder.Entity<TeamTransfer>(entity =>
        {
            entity.HasOne(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.FromTeam).WithMany()
                .HasForeignKey(t => t.FromTeamId).OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.ToTeam).WithMany()
                .HasForeignKey(t => t.ToTeamId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(t => new { t.CompanyId, t.EffectiveOn });
            entity.HasIndex(t => t.UserId);

            entity.Property(t => t.MovedByName).HasMaxLength(120);
        });

        /* ---------------- statutory billing ---------------- */

        modelBuilder.Entity<GstProfile>(entity =>
        {
            entity.HasOne(p => p.Project).WithMany()
                .HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);

            // One profile per project, and one company-wide fallback. Two
            // active profiles for the same project would make the GSTIN on an
            // invoice depend on row order.
            entity.HasIndex(p => new { p.CompanyId, p.ProjectId }).IsUnique();

            entity.Property(p => p.Gstin).HasMaxLength(15);
            entity.Property(p => p.Pan).HasMaxLength(10);
            entity.Property(p => p.StateCode).HasMaxLength(2);
            entity.Property(p => p.StateName).HasMaxLength(64);
            entity.Property(p => p.LegalName).HasMaxLength(200);
            entity.Property(p => p.TradeName).HasMaxLength(200);
            entity.Property(p => p.DefaultTreatment).HasMaxLength(40);
            entity.Property(p => p.DefaultSacCode).HasMaxLength(10);
            entity.Property(p => p.InvoicePrefix).HasMaxLength(16);
            entity.Property(p => p.CreditNotePrefix).HasMaxLength(16);
            entity.Property(p => p.BankIfsc).HasMaxLength(11);
        });

        modelBuilder.Entity<TaxInvoice>(entity =>
        {
            entity.HasOne(i => i.Booking).WithMany()
                .HasForeignKey(i => i.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Demand).WithMany()
                .HasForeignKey(i => i.DemandId).OnDelete(DeleteBehavior.Cascade);

            // The series has to be unbroken and unique within the company —
            // the law says so, and a duplicated number is a document two
            // customers can both claim credit against.
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique();
            entity.HasIndex(i => new { i.CompanyId, i.Status, i.InvoiceDate });
            entity.HasIndex(i => i.DemandId);

            entity.Property(i => i.InvoiceNumber).HasMaxLength(48);
            entity.Property(i => i.Treatment).HasMaxLength(40);
            entity.Property(i => i.SacCode).HasMaxLength(10);
            entity.Property(i => i.Status).HasMaxLength(16);
            entity.Property(i => i.CustomerName).HasMaxLength(200);
            entity.Property(i => i.CustomerGstin).HasMaxLength(15);
            entity.Property(i => i.CustomerPan).HasMaxLength(10);
            entity.Property(i => i.PlaceOfSupply).HasMaxLength(64);

            // Computed on read from the stored columns. Mapping them would
            // create columns that can disagree with the parts they add up.
            entity.Ignore(i => i.TotalTax);
            entity.Ignore(i => i.InvoiceTotal);
        });

        modelBuilder.Entity<CreditNote>(entity =>
        {
            entity.HasOne(c => c.Booking).WithMany()
                .HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.TaxInvoice).WithMany()
                .HasForeignKey(c => c.TaxInvoiceId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => new { c.CompanyId, c.CreditNoteNumber }).IsUnique();
            entity.HasIndex(c => c.TaxInvoiceId);

            entity.Property(c => c.CreditNoteNumber).HasMaxLength(48);
            entity.Property(c => c.Reason).HasMaxLength(32);

            entity.Ignore(c => c.TotalTax);
            entity.Ignore(c => c.CreditTotal);
        });

        modelBuilder.Entity<PostDatedCheque>(entity =>
        {
            entity.HasOne(p => p.Booking).WithMany()
                .HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);

            // The banking run reads exactly this: held cheques, by date.
            entity.HasIndex(p => new { p.CompanyId, p.Status, p.ChequeDate });
            entity.HasIndex(p => p.BookingId);

            entity.Property(p => p.ChequeNumber).HasMaxLength(24);
            entity.Property(p => p.BankName).HasMaxLength(120);
            entity.Property(p => p.BranchName).HasMaxLength(120);
            entity.Property(p => p.Status).HasMaxLength(16);
        });

        modelBuilder.Entity<TdsCertificate>(entity =>
        {
            entity.HasOne(t => t.Booking).WithMany()
                .HasForeignKey(t => t.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Receipt).WithMany()
                .HasForeignKey(t => t.ReceiptId).OnDelete(DeleteBehavior.Cascade);

            // One tracking row per receipt, so a re-run of the sync cannot
            // double the amount the developer believes it is owed.
            entity.HasIndex(t => t.ReceiptId).IsUnique();
            entity.HasIndex(t => new { t.CompanyId, t.Status, t.Quarter });

            entity.Property(t => t.Status).HasMaxLength(16);
            entity.Property(t => t.Quarter).HasMaxLength(16);
            entity.Property(t => t.DeductorPan).HasMaxLength(10);
            entity.Property(t => t.DeductorName).HasMaxLength(200);
            entity.Property(t => t.CertificateNumber).HasMaxLength(48);
            entity.Property(t => t.ChallanNumber).HasMaxLength(48);
        });

        modelBuilder.Entity<Demand>(entity =>
        {
            entity.HasOne(d => d.Booking).WithMany(b => b.Demands)
                .HasForeignKey(d => d.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.CompanyId, d.DemandNumber }).IsUnique();
            entity.HasIndex(d => new { d.CompanyId, d.Status, d.DueDate });

            entity.Property(d => d.DemandNumber).HasMaxLength(48);
            entity.Property(d => d.Label).HasMaxLength(160);
            entity.Property(d => d.Status).HasMaxLength(16);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasOne(r => r.Booking).WithMany(b => b.Receipts)
                .HasForeignKey(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.CompanyId, r.ReceiptNumber }).IsUnique();
            entity.HasIndex(r => new { r.CompanyId, r.ReceivedOn });

            entity.Property(r => r.ReceiptNumber).HasMaxLength(48);
            entity.Property(r => r.Mode).HasMaxLength(24);
            entity.Property(r => r.Status).HasMaxLength(16);
            entity.Property(r => r.Instrument).HasMaxLength(64);
            entity.Property(r => r.BankName).HasMaxLength(120);
        });

        modelBuilder.Entity<ReceiptAllocation>(entity =>
        {
            entity.HasOne(a => a.Receipt).WithMany(r => r.Allocations)
                .HasForeignKey(a => a.ReceiptId).OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Demand).WithMany()
                .HasForeignKey(a => a.DemandId).OnDelete(DeleteBehavior.Cascade);

            // One row per receipt per demand. Two would double-count a payment.
            entity.HasIndex(a => new { a.ReceiptId, a.DemandId }).IsUnique();
        });

        modelBuilder.Entity<BookingAgreement>(entity =>
        {
            // One agreement per booking — this is the file, not a list of drafts.
            entity.HasIndex(a => a.BookingId).IsUnique();

            entity.HasOne(a => a.Booking).WithMany()
                .HasForeignKey(a => a.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(a => a.Status).HasMaxLength(24);
            entity.Property(a => a.RegistrationNumber).HasMaxLength(64);
            entity.Property(a => a.SubRegistrarOffice).HasMaxLength(160);
        });

        modelBuilder.Entity<Possession>(entity =>
        {
            entity.HasIndex(p => p.BookingId).IsUnique();

            entity.HasOne(p => p.Booking).WithMany()
                .HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Status).HasMaxLength(24);
        });

        modelBuilder.Entity<HomeLoan>(entity =>
        {
            entity.HasOne(l => l.Booking).WithMany()
                .HasForeignKey(l => l.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(l => l.BankName).HasMaxLength(160);
            entity.Property(l => l.Status).HasMaxLength(24);
            entity.Property(l => l.ApplicationNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<BookingCancellation>(entity =>
        {
            entity.HasOne(c => c.Booking).WithMany()
                .HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(c => c.Status).HasMaxLength(16);
        });

        modelBuilder.Entity<BookingTransfer>(entity =>
        {
            entity.HasOne(t => t.Booking).WithMany()
                .HasForeignKey(t => t.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(t => t.Status).HasMaxLength(16);
            entity.Property(t => t.FromName).HasMaxLength(160);
            entity.Property(t => t.ToName).HasMaxLength(160);
        });

        modelBuilder.Entity<Brokerage>(entity =>
        {
            entity.HasOne(b => b.Booking).WithMany()
                .HasForeignKey(b => b.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(b => b.PartnerName).HasMaxLength(160);
            entity.Property(b => b.Status).HasMaxLength(16);
        });

        modelBuilder.Entity<BrokerageSlab>(entity =>
        {
            entity.HasOne(s => s.Brokerage).WithMany(b => b.Slabs)
                .HasForeignKey(s => s.BrokerageId).OnDelete(DeleteBehavior.Cascade);

            entity.Property(s => s.Label).HasMaxLength(120);
            entity.Property(s => s.Trigger).HasMaxLength(24);
        });

        modelBuilder.Entity<BookingDocument>(entity =>
        {
            entity.HasOne(d => d.Booking).WithMany()
                .HasForeignKey(d => d.BookingId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.BookingId, d.Stage });

            entity.Property(d => d.Key).HasMaxLength(64);
            entity.Property(d => d.Name).HasMaxLength(160);
            entity.Property(d => d.Stage).HasMaxLength(24);
            entity.Property(d => d.Status).HasMaxLength(24);
            entity.Property(d => d.FileUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<EscrowEntry>(entity =>
        {
            entity.HasOne(e => e.Receipt).WithMany()
                .HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Cascade);

            // The compliance question: what came in on this project, this period.
            entity.HasIndex(e => new { e.CompanyId, e.ProjectId, e.On });
        });
    }

    /* ------------------------------------------------------------------ *
     * Foundation
     * ------------------------------------------------------------------ */
    private static void ConfigureFoundation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasOne(b => b.Company)
                .WithMany(c => c.Branches)
                .HasForeignKey(b => b.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => b.CompanyId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.CompanyId);

            entity.HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserBranch>(entity =>
        {
            entity.HasKey(ub => new { ub.UserId, ub.BranchId });

            entity.HasOne(ub => ub.User)
                .WithMany(u => u.UserBranches)
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ub => ub.Branch)
                .WithMany(b => b.UserBranches)
                .HasForeignKey(ub => ub.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /* ------------------------------------------------------------------ *
     * Leads
     * ------------------------------------------------------------------ */
    private static void ConfigureLeads(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>(entity =>
        {
            entity.HasIndex(l => l.CompanyId);
            entity.HasIndex(l => new { l.CompanyId, l.Stage });
            entity.HasIndex(l => new { l.CompanyId, l.OwnerId });
            entity.HasIndex(l => new { l.CompanyId, l.CreatedAt });
            entity.HasIndex(l => l.Phone);
            entity.HasIndex(l => l.Email);

            entity.HasOne(l => l.Company)
                .WithMany()
                .HasForeignKey(l => l.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Branch)
                .WithMany()
                .HasForeignKey(l => l.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Owner)
                .WithMany()
                .HasForeignKey(l => l.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.SupportingManager)
                .WithMany()
                .HasForeignKey(l => l.SupportingManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.InterestedProject)
                .WithMany()
                .HasForeignKey(l => l.InterestedProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeadActivity>(entity =>
        {
            entity.HasIndex(a => a.LeadId);
            entity.HasIndex(a => a.CreatedAt);

            entity.HasOne(a => a.Lead)
                .WithMany(l => l.Activities)
                .HasForeignKey(a => a.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mirrors the parent's filter. Without it EF warns that a required
            // navigation could point at a row the filter has hidden — and an
            // orphaned timeline entry would be exactly that.
            entity.HasQueryFilter(a => !a.Lead!.IsDeleted);
        });
    }

    /* ------------------------------------------------------------------ *
     * CRM objects
     * ------------------------------------------------------------------ */
    private static void ConfigureCrm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasIndex(c => new { c.CompanyId, c.Type });
            entity.HasIndex(c => new { c.CompanyId, c.LifecycleStage });
            entity.HasIndex(c => c.Phone);
            entity.HasIndex(c => c.Email);

            entity.HasOne(c => c.Branch).WithMany()
                .HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Owner).WithMany()
                .HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Opportunity>(entity =>
        {
            entity.HasIndex(o => new { o.CompanyId, o.Stage });
            entity.HasIndex(o => new { o.CompanyId, o.ExpectedCloseDate });
            entity.HasIndex(o => new { o.CompanyId, o.OwnerId });

            entity.HasOne(o => o.Branch).WithMany()
                .HasForeignKey(o => o.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Contact).WithMany(c => c.Opportunities)
                .HasForeignKey(o => o.ContactId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(o => o.Owner).WithMany()
                .HasForeignKey(o => o.OwnerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(o => o.Project).WithMany()
                .HasForeignKey(o => o.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(o => o.Unit).WithMany()
                .HasForeignKey(o => o.UnitId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FollowUp>(entity =>
        {
            entity.HasIndex(f => new { f.CompanyId, f.Status, f.DueAt });
            entity.HasIndex(f => new { f.CompanyId, f.OwnerId });
            entity.HasIndex(f => new { f.RelatedType, f.RelatedId });

            entity.HasOne(f => f.Branch).WithMany()
                .HasForeignKey(f => f.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(f => f.Owner).WithMany()
                .HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            // Goals are read as "what is live for this scope right now", so the
            // index leads with the window rather than the name.
            entity.HasIndex(g => new { g.CompanyId, g.Status, g.EndDate });
            entity.HasIndex(g => new { g.CompanyId, g.ScopeType, g.OwnerId });

            entity.Property(g => g.TargetValue).HasPrecision(18, 2);

            entity.HasOne(g => g.Branch).WithMany()
                .HasForeignKey(g => g.BranchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(g => g.Owner).WithMany()
                .HasForeignKey(g => g.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CallLog>(entity =>
        {
            entity.HasIndex(c => new { c.CompanyId, c.StartedAt });
            entity.HasIndex(c => new { c.RelatedType, c.RelatedId });
            entity.HasIndex(c => new { c.CompanyId, c.AgentId });

            entity.HasOne(c => c.Branch).WithMany()
                .HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteVisit>(entity =>
        {
            entity.HasIndex(v => new { v.CompanyId, v.Status });
            entity.HasIndex(v => new { v.CompanyId, v.ScheduledAt });
            entity.HasIndex(v => v.VisitCode).IsUnique();
            entity.HasIndex(v => v.LeadId);

            entity.HasOne(v => v.Branch).WithMany()
                .HasForeignKey(v => v.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.Project).WithMany()
                .HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(v => v.Unit).WithMany()
                .HasForeignKey(v => v.UnitId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ObmVisit>(entity =>
        {
            entity.HasIndex(v => new { v.CompanyId, v.Status });
            entity.HasIndex(v => new { v.CompanyId, v.ScheduledAt });
            entity.HasIndex(v => v.VisitCode).IsUnique();

            // The lead grid reads the latest OBM per lead on every page render.
            entity.HasIndex(v => v.LeadId);

            entity.HasOne(v => v.Branch).WithMany()
                .HasForeignKey(v => v.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.HasIndex(q => new { q.CompanyId, q.Status });
            entity.HasIndex(q => q.QuoteNumber).IsUnique();

            entity.HasOne(q => q.Branch).WithMany()
                .HasForeignKey(q => q.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Owner).WithMany()
                .HasForeignKey(q => q.OwnerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(q => q.Project).WithMany()
                .HasForeignKey(q => q.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(q => q.Unit).WithMany()
                .HasForeignKey(q => q.UnitId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(q => q.PaymentPlan).WithMany()
                .HasForeignKey(q => q.PaymentPlanId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(q => new { q.CompanyId, q.ApprovalStatus });
        });

        modelBuilder.Entity<QuotationMilestone>(entity =>
        {
            entity.HasIndex(m => m.QuotationId);

            entity.HasOne(m => m.Quotation)
                .WithMany(q => q.Milestones)
                .HasForeignKey(m => m.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(m => !m.Quotation!.IsDeleted);
        });

        // The three below hang off a soft-deletable parent and carried no
        // matching filter, which EF flags on every start. It is not only noise:
        // a deleted quotation kept a visible timeline and — the one that
        // matters — a live share link, so a document withdrawn from a customer
        // still resolved through the row that let them read it.
        modelBuilder.Entity<QuotationActivity>(entity =>
        {
            entity.HasIndex(a => a.QuotationId);

            entity.HasOne(a => a.Quotation)
                .WithMany(q => q.Activities)
                .HasForeignKey(a => a.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(a => !a.Quotation!.IsDeleted);
        });

        modelBuilder.Entity<QuotationShareLink>(entity =>
        {
            entity.HasIndex(l => l.Token).IsUnique();

            entity.HasOne(l => l.Quotation)
                .WithMany(q => q.ShareLinks)
                .HasForeignKey(l => l.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(l => !l.Quotation!.IsDeleted);
        });

        modelBuilder.Entity<QuotationTemplateCharge>(entity =>
        {
            entity.HasIndex(c => c.TemplateId);

            entity.HasOne(c => c.Template)
                .WithMany(t => t.Charges)
                .HasForeignKey(c => c.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(c => !c.Template!.IsDeleted);
        });

        modelBuilder.Entity<RateCard>(entity =>
        {
            // The card lookup is "newest card at or before this date" on every
            // quote, so the index has to carry the date.
            entity.HasIndex(c => new { c.CompanyId, c.ProjectId, c.EffectiveFrom });

            entity.HasOne(c => c.Project).WithMany()
                .HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.Tower).WithMany()
                .HasForeignKey(c => c.TowerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PaymentPlan>(entity =>
        {
            entity.HasIndex(p => new { p.CompanyId, p.Code });

            entity.HasOne(p => p.Project).WithMany()
                .HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentPlanMilestone>(entity =>
        {
            entity.HasIndex(m => m.PaymentPlanId);

            entity.HasOne(m => m.PaymentPlan)
                .WithMany(p => p.Milestones)
                .HasForeignKey(m => m.PaymentPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(m => !m.PaymentPlan!.IsDeleted);
        });

        modelBuilder.Entity<UnitStatusHistory>(entity =>
        {
            entity.HasIndex(h => h.UnitId);

            entity.HasOne(h => h.Unit)
                .WithMany(u => u.History)
                .HasForeignKey(h => h.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(h => !h.Unit!.IsDeleted);
        });

        modelBuilder.Entity<Approval>(entity =>
        {
            // The approver's queue is "pending, mine, newest first"; the second
            // index serves the badge on whichever record is open.
            entity.HasIndex(a => new { a.CompanyId, a.Status, a.RequestedAt });
            entity.HasIndex(a => new { a.EntityType, a.EntityId });

            entity.HasOne(a => a.Branch).WithMany()
                .HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuotationLine>(entity =>
        {
            entity.HasIndex(l => l.QuotationId);

            entity.HasOne(l => l.Quotation)
                .WithMany(q => q.Lines)
                .HasForeignKey(l => l.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(l => !l.Quotation!.IsDeleted);
        });

        modelBuilder.Entity<ChargeHead>(entity =>
        {
            entity.HasIndex(c => new { c.CompanyId, c.ProjectId, c.SortOrder });
            entity.HasIndex(c => new { c.CompanyId, c.Code });

            entity.HasOne(c => c.Project).WithMany()
                .HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuotationCharge>(entity =>
        {
            entity.HasIndex(c => c.QuotationId);

            entity.HasOne(c => c.Quotation)
                .WithMany(q => q.Charges)
                .HasForeignKey(c => c.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(c => !c.Quotation!.IsDeleted);
        });
    }

    /* ------------------------------------------------------------------ *
     * Inventory
     * ------------------------------------------------------------------ */
    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(p => new { p.CompanyId, p.Status });
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
        });

        modelBuilder.Entity<Tower>(entity =>
        {
            entity.HasIndex(t => t.ProjectId);

            entity.HasOne(t => t.Project)
                .WithMany(p => p.Towers)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(t => !t.Project!.IsDeleted);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasIndex(u => new { u.CompanyId, u.Status });
            entity.HasIndex(u => new { u.ProjectId, u.UnitNumber }).IsUnique();
            entity.HasIndex(u => new { u.CompanyId, u.Configuration });

            entity.HasOne(u => u.Project)
                .WithMany(p => p.Units)
                .HasForeignKey(u => u.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.Tower)
                .WithMany(t => t.Units)
                .HasForeignKey(u => u.TowerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SpaceBooking>(entity =>
        {
            // The availability question — "what is taken in this space over this
            // window?" — is the one this table exists to answer, and it is asked
            // on every calendar render, so it gets the leading index.
            entity.HasIndex(b => new { b.UnitId, b.EventDate });

            // The venue-wide calendar and the day sheet, in that order.
            entity.HasIndex(b => new { b.CompanyId, b.EventDate });
            entity.HasIndex(b => new { b.ProjectId, b.EventDate });

            // Releasing or moving a multi-day run reads every row by its ref.
            entity.HasIndex(b => new { b.CompanyId, b.GroupRef });

            entity.HasOne(b => b.Unit)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Project)
                .WithMany()
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // A lead can be deleted after its dates are held; the hold survives
            // as an anonymous block rather than taking the calendar with it.
            entity.HasOne(b => b.Lead)
                .WithMany()
                .HasForeignKey(b => b.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(b => !b.Unit!.IsDeleted);
        });

        modelBuilder.Entity<VenuePackage>(entity =>
        {
            entity.HasIndex(p => new { p.CompanyId, p.ProjectId, p.Code });
            entity.HasQueryFilter(p => !p.IsDeleted);
            entity.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VenuePackageSpace>(entity =>
        {
            entity.HasIndex(s => new { s.VenuePackageId, s.UnitId }).IsUnique();
            entity.HasOne(s => s.Package)
                .WithMany(p => p.Spaces)
                .HasForeignKey(s => s.VenuePackageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Unit)
                .WithMany()
                .HasForeignKey(s => s.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VenuePeakDate>(entity =>
        {
            entity.HasIndex(p => new { p.ProjectId, p.StartDate, p.EndDate });
            entity.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /* ------------------------------------------------------------------ *
     * Rentable asset inventory
     *
     * The indexes here are chosen for two queries and nothing else: the
     * catalogue grid (company + category, filtered by status) and the
     * availability check (item + date window). The second is the hot one —
     * it runs once per item per quote line — so PropReservation gets the
     * leading composite index on the exact columns the overlap test reads.
     * ------------------------------------------------------------------ */
    private static void ConfigureProps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PropStore>(entity =>
        {
            entity.HasIndex(s => new { s.CompanyId, s.Code }).IsUnique();

            entity.HasOne(s => s.Keeper)
                .WithMany()
                .HasForeignKey(s => s.KeeperId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PropCategory>(entity =>
        {
            entity.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
            entity.HasIndex(c => new { c.CompanyId, c.SortOrder });

            // Restrict rather than cascade: deleting a section must not take
            // its sub-sections — and their items — silently with it.
            entity.HasOne(c => c.Parent)
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PropItem>(entity =>
        {
            entity.HasIndex(i => new { i.CompanyId, i.Code }).IsUnique();
            entity.HasIndex(i => new { i.CompanyId, i.CategoryId, i.Status });
            entity.HasIndex(i => new { i.CompanyId, i.ItemType });
            entity.HasIndex(i => new { i.CompanyId, i.StoreId });

            entity.HasOne(i => i.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Store)
                .WithMany()
                .HasForeignKey(i => i.StoreId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.Owner)
                .WithMany()
                .HasForeignKey(i => i.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PropItemPhoto>(entity =>
        {
            entity.HasIndex(p => new { p.PropItemId, p.SortOrder });

            entity.HasOne(p => p.Item)
                .WithMany(i => i.Photos)
                .HasForeignKey(p => p.PropItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // A retired item's photos go with it. Without this the photo table
            // is the one place a soft-deleted line still answers a query.
            entity.HasQueryFilter(p => !p.Item!.IsDeleted);
        });

        modelBuilder.Entity<PropStockMovement>(entity =>
        {
            // The item's own ledger, newest first — the stock card view.
            entity.HasIndex(m => new { m.PropItemId, m.MovedAt });
            entity.HasIndex(m => new { m.CompanyId, m.MovedAt });
            entity.HasIndex(m => new { m.CompanyId, m.MovementType });

            entity.HasOne(m => m.Item)
                .WithMany(i => i.Movements)
                .HasForeignKey(m => m.PropItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // The ledger outlives the gate pass it came from. A deleted pass
            // must not erase the record that stock left the building.
            entity.HasOne(m => m.Issue)
                .WithMany()
                .HasForeignKey(m => m.PropIssueId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PropIssue>(entity =>
        {
            entity.HasIndex(i => new { i.CompanyId, i.Code }).IsUnique();
            entity.HasIndex(i => new { i.CompanyId, i.Status });

            // The return chaser: everything due back on or before a date.
            entity.HasIndex(i => new { i.CompanyId, i.ExpectedReturnDate });
            entity.HasIndex(i => new { i.CompanyId, i.DispatchDate });
            entity.HasIndex(i => new { i.CompanyId, i.LeadId });

            entity.HasOne(i => i.Lead)
                .WithMany()
                .HasForeignKey(i => i.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.Project)
                .WithMany()
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.Store)
                .WithMany()
                .HasForeignKey(i => i.StoreId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.SiteInCharge)
                .WithMany()
                .HasForeignKey(i => i.SiteInChargeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PropIssueLine>(entity =>
        {
            entity.HasIndex(l => new { l.PropIssueId, l.PropItemId }).IsUnique();

            entity.HasOne(l => l.Issue)
                .WithMany(i => i.Lines)
                .HasForeignKey(l => l.PropIssueId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.PropItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(l => !l.Issue!.IsDeleted);
        });

        modelBuilder.Entity<PropKit>(entity =>
        {
            entity.HasIndex(k => new { k.CompanyId, k.Code }).IsUnique();
            entity.HasIndex(k => new { k.CompanyId, k.IsActive });
        });

        modelBuilder.Entity<PropKitLine>(entity =>
        {
            entity.HasIndex(l => new { l.PropKitId, l.PropItemId }).IsUnique();

            entity.HasOne(l => l.Kit)
                .WithMany(k => k.Lines)
                .HasForeignKey(l => l.PropKitId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not cascade: retiring one vase must not silently gut
            // every kit that used it — the kit has to be edited deliberately.
            entity.HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.PropItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(l => !l.Kit!.IsDeleted);
        });

        modelBuilder.Entity<PropReservation>(entity =>
        {
            // "What is held on this item across this window?" — asked once per
            // item per quote line, so it leads on exactly those three columns.
            entity.HasIndex(r => new { r.PropItemId, r.FromDate, r.ToDate });
            entity.HasIndex(r => new { r.CompanyId, r.Status, r.FromDate });
            entity.HasIndex(r => new { r.CompanyId, r.LeadId });

            entity.HasOne(r => r.Item)
                .WithMany()
                .HasForeignKey(r => r.PropItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Issue)
                .WithMany()
                .HasForeignKey(r => r.PropIssueId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /* ------------------------------------------------------------------ *
     * Vendors, crew and fleet
     *
     * Three resources, one index pattern. Each has a blocking-rows table whose
     * leading index is (resource, from, to) — the columns the overlap test
     * reads — because "who is free on the 14th?" is asked once per candidate
     * per screen, and it is the only query on these tables that runs hot.
     * ------------------------------------------------------------------ */
    private static void ConfigureResources(ModelBuilder modelBuilder)
    {
        /* ---------------- vendors ---------------- */

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasIndex(v => new { v.CompanyId, v.Code }).IsUnique();
            entity.HasIndex(v => new { v.CompanyId, v.Status });
            entity.HasIndex(v => new { v.CompanyId, v.City });

            entity.HasOne(v => v.Owner)
                .WithMany()
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VendorRate>(entity =>
        {
            entity.HasIndex(r => new { r.VendorId, r.Service });

            entity.HasOne(r => r.Vendor)
                .WithMany(v => v.Rates)
                .HasForeignKey(r => r.VendorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(r => !r.Vendor!.IsDeleted);
        });

        modelBuilder.Entity<VendorDocument>(entity =>
        {
            // The expiry sweep — "whose licence lapses this month?" — reads on
            // this, so it leads on the date rather than on the vendor.
            entity.HasIndex(d => new { d.CompanyId, d.ExpiryDate });

            entity.HasOne(d => d.Vendor)
                .WithMany(v => v.Documents)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(d => !d.Vendor!.IsDeleted);
        });

        modelBuilder.Entity<VendorPurchaseOrder>(entity =>
        {
            entity.HasIndex(o => new { o.CompanyId, o.Code }).IsUnique();

            // The vendor availability check.
            entity.HasIndex(o => new { o.VendorId, o.ServiceDate, o.ServiceEndDate });

            entity.HasIndex(o => new { o.CompanyId, o.Status });
            entity.HasIndex(o => new { o.CompanyId, o.LeadId });
            entity.HasIndex(o => new { o.CompanyId, o.ServiceDate });

            entity.HasOne(o => o.Vendor)
                .WithMany()
                .HasForeignKey(o => o.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Lead)
                .WithMany()
                .HasForeignKey(o => o.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.Project)
                .WithMany()
                .HasForeignKey(o => o.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.Coordinator)
                .WithMany()
                .HasForeignKey(o => o.CoordinatorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VendorPoLine>(entity =>
        {
            entity.HasIndex(l => new { l.VendorPurchaseOrderId, l.SortOrder });

            entity.HasOne(l => l.Order)
                .WithMany(o => o.Lines)
                .HasForeignKey(l => l.VendorPurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // The rate card can be retired while an old order still quotes it.
            entity.HasOne(l => l.RateCard)
                .WithMany()
                .HasForeignKey(l => l.VendorRateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(l => !l.Order!.IsDeleted);
        });

        modelBuilder.Entity<VendorPayment>(entity =>
        {
            entity.HasIndex(p => new { p.VendorPurchaseOrderId, p.PaidOn });
            entity.HasIndex(p => new { p.CompanyId, p.PaidOn });

            entity.HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.VendorPurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(p => !p.Order!.IsDeleted);
        });

        /* ---------------- crew ---------------- */

        modelBuilder.Entity<CrewMember>(entity =>
        {
            entity.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
            entity.HasIndex(c => new { c.CompanyId, c.PrimaryRole, c.Status });
            entity.HasIndex(c => new { c.CompanyId, c.EngagementType });

            entity.HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.SupplierVendor)
                .WithMany()
                .HasForeignKey(c => c.SupplierVendorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CrewAssignment>(entity =>
        {
            // The crew availability check.
            entity.HasIndex(a => new { a.CrewMemberId, a.FromDate, a.ToDate });

            entity.HasIndex(a => new { a.CompanyId, a.Status, a.FromDate });
            entity.HasIndex(a => new { a.CompanyId, a.LeadId });
            entity.HasIndex(a => new { a.CompanyId, a.FromDate });

            entity.HasOne(a => a.CrewMember)
                .WithMany()
                .HasForeignKey(a => a.CrewMemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Lead)
                .WithMany()
                .HasForeignKey(a => a.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Project)
                .WithMany()
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(a => !a.CrewMember!.IsDeleted);
        });

        /* ---------------- fleet ---------------- */

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(v => new { v.CompanyId, v.RegistrationNumber }).IsUnique();
            entity.HasIndex(v => new { v.CompanyId, v.Status });

            entity.HasOne(v => v.SupplierVendor)
                .WithMany()
                .HasForeignKey(v => v.SupplierVendorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(v => v.DefaultDriver)
                .WithMany()
                .HasForeignKey(v => v.DefaultDriverCrewId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(v => v.Store)
                .WithMany()
                .HasForeignKey(v => v.StoreId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VehicleTrip>(entity =>
        {
            entity.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique();

            // The vehicle availability check.
            entity.HasIndex(t => new { t.VehicleId, t.FromDate, t.ToDate });

            entity.HasIndex(t => new { t.CompanyId, t.Status, t.FromDate });
            entity.HasIndex(t => new { t.CompanyId, t.FromDate });

            entity.HasOne(t => t.Vehicle)
                .WithMany()
                .HasForeignKey(t => t.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Driver)
                .WithMany()
                .HasForeignKey(t => t.DriverCrewId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Lead)
                .WithMany()
                .HasForeignKey(t => t.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Project)
                .WithMany()
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VehicleTripLoad>(entity =>
        {
            entity.HasIndex(l => new { l.VehicleTripId, l.PropIssueId }).IsUnique();

            entity.HasOne(l => l.Trip)
                .WithMany(t => t.Loads)
                .HasForeignKey(l => l.VehicleTripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Issue)
                .WithMany()
                .HasForeignKey(l => l.PropIssueId)
                .OnDelete(DeleteBehavior.Cascade);

            // A retired gate pass takes its place on the manifest with it.
            entity.HasQueryFilter(l => !l.Issue!.IsDeleted);
        });

        /* ---------------- the resource plan behind a proposal ---------------- */

        modelBuilder.Entity<QuotationResource>(entity =>
        {
            entity.HasIndex(r => new { r.QuotationId, r.SortOrder });
            entity.HasIndex(r => new { r.CompanyId, r.State });

            // Every link is Restrict or SetNull, never Cascade: a proposal's
            // resource plan is a priced record of an offer that was made, and
            // retiring a prop or a supplier must not quietly rewrite it.
            entity.HasOne(r => r.Quotation)
                .WithMany()
                .HasForeignKey(r => r.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.PropItem)
                .WithMany()
                .HasForeignKey(r => r.PropItemId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.PropKit)
                .WithMany()
                .HasForeignKey(r => r.PropKitId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.CrewMember)
                .WithMany()
                .HasForeignKey(r => r.CrewMemberId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.Vendor)
                .WithMany()
                .HasForeignKey(r => r.VendorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.VendorRate)
                .WithMany()
                .HasForeignKey(r => r.VendorRateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(r => !r.Quotation!.IsDeleted);
        });
    }

    private static void ConfigureHr(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HrDepartment>(e =>
            e.HasIndex(d => new { d.CompanyId, d.Name }));

        modelBuilder.Entity<HrEmployee>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.EmployeeCode }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.Status });
            e.HasOne(x => x.Manager)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.ManagerEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HrAttendance>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique());

        modelBuilder.Entity<HrLeaveBalance>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique());

        /* ---------------- leave allocation ---------------- */
        //
        // The indexes are the questions asked of these tables: which period
        // covers this date, what has this employee been granted this period,
        // and is anybody blocked from taking leave on these days.

        modelBuilder.Entity<HrLeavePeriod>(e =>
            e.HasIndex(x => new { x.CompanyId, x.FromDate, x.ToDate }));

        modelBuilder.Entity<HrLeavePolicyLine>(e =>
        {
            e.HasOne(x => x.LeavePolicy).WithMany(p => p.Lines)
                .HasForeignKey(x => x.LeavePolicyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.LeavePolicyId, x.LeaveTypeId }).IsUnique();
        });

        modelBuilder.Entity<HrLeavePolicyAssignment>(e =>
            // One policy per employee per period. A second one would grant
            // twice and there would be no way to tell which was meant.
            e.HasIndex(x => new { x.EmployeeId, x.LeavePeriodId }).IsUnique());

        modelBuilder.Entity<HrLeaveAllocation>(e =>
        {
            e.HasOne(x => x.LeavePeriod).WithMany(p => p.Allocations)
                .HasForeignKey(x => x.LeavePeriodId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EmployeeId, x.LeavePeriodId, x.LeaveTypeId });
        });

        modelBuilder.Entity<HrCompensatoryRequest>(e =>
            // A day worked can only be claimed back once.
            e.HasIndex(x => new { x.EmployeeId, x.WorkedOn }).IsUnique());

        modelBuilder.Entity<HrLeaveBlockDate>(e =>
            e.HasIndex(x => new { x.CompanyId, x.FromDate, x.ToDate }));

        modelBuilder.Entity<HrShiftAssignment>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.FromDate, x.ToDate }));

        modelBuilder.Entity<HrShiftRequest>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.Status }));

        modelBuilder.Entity<HrAttendanceRequest>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.FromDate, x.ToDate }));

        /* ---------------- salary components ---------------- */

        modelBuilder.Entity<HrSalaryComponent>(e =>
            // Abbreviations are what formulas refer to, so two components
            // sharing one would make a formula ambiguous.
            e.HasIndex(x => new { x.CompanyId, x.Abbreviation }).IsUnique());

        modelBuilder.Entity<HrPayStructureLine>(e =>
        {
            e.HasOne(x => x.PayStructure).WithMany(s => s.Lines)
                .HasForeignKey(x => x.PayStructureId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PayStructureId, x.SalaryComponentId }).IsUnique();
        });

        modelBuilder.Entity<HrPayStructureAssignment>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }));

        modelBuilder.Entity<HrAdditionalSalary>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }));

        modelBuilder.Entity<HrEmployeeAdvance>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.Status }));

        modelBuilder.Entity<HrAdvanceRepayment>(e =>
        {
            e.HasOne(x => x.EmployeeAdvance).WithMany(a => a.Repayments)
                .HasForeignKey(x => x.EmployeeAdvanceId)
                .OnDelete(DeleteBehavior.Cascade);
            // One recovery per advance per run. Reprocessing a month must not
            // take a second instalment.
            e.HasIndex(x => new { x.EmployeeAdvanceId, x.PayrollRunId }).IsUnique();
        });

        /* ---------------- talent ---------------- */

        modelBuilder.Entity<HrStaffingPlanLine>(e =>
        {
            e.HasOne(x => x.StaffingPlan).WithMany(p => p.Lines)
                .HasForeignKey(x => x.StaffingPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.StaffingPlanId, x.DepartmentId });
        });

        modelBuilder.Entity<HrJobRequisition>(e =>
            e.HasIndex(x => new { x.CompanyId, x.Status }));

        modelBuilder.Entity<HrInterviewSkill>(e =>
        {
            e.HasOne(x => x.InterviewRound).WithMany(r => r.Skills)
                .HasForeignKey(x => x.InterviewRoundId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.InterviewRoundId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<HrInterviewFeedback>(e =>
            // One scorecard per panellist per interview. A second would be an
            // edit, and averaging two of them would quietly double a vote.
            e.HasIndex(x => new { x.InterviewId, x.PanellistName }).IsUnique());

        modelBuilder.Entity<HrInterviewSkillRating>(e =>
        {
            e.HasOne(x => x.InterviewFeedback).WithMany(f => f.Ratings)
                .HasForeignKey(x => x.InterviewFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.InterviewFeedbackId);
        });

        modelBuilder.Entity<HrJobOffer>(e =>
            e.HasIndex(x => new { x.CandidateId, x.Revision }).IsUnique());

        modelBuilder.Entity<HrEmployeeReferral>(e =>
            e.HasIndex(x => new { x.ReferrerEmployeeId, x.Status }));

        modelBuilder.Entity<HrAppraisalTemplateKra>(e =>
        {
            e.HasOne(x => x.AppraisalTemplate).WithMany(t => t.Kras)
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AppraisalTemplateId, x.Title }).IsUnique();
        });

        modelBuilder.Entity<HrAppraisalKra>(e =>
        {
            e.HasOne(x => x.Appraisal).WithMany(a => a.Kras)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.AppraisalId);
        });

        modelBuilder.Entity<HrPerformanceFeedback>(e =>
        {
            // Two people are involved and only one of them may cascade —
            // deleting an employee must not silently remove the feedback they
            // gave about somebody still on the rolls.
            e.HasOne(x => x.Employee).WithMany()
                .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GivenByEmployee).WithMany()
                .HasForeignKey(x => x.GivenByEmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmployeeId, x.AppraisalCycleId });
        });

        modelBuilder.Entity<HrTrainingFeedback>(e =>
            e.HasIndex(x => new { x.TrainingId, x.EmployeeId }).IsUnique());

        /* ---------------- the workplace ---------------- */

        modelBuilder.Entity<HrChecklistTask>(e =>
        {
            e.HasOne(x => x.ChecklistTemplate).WithMany(t => t.Tasks)
                .HasForeignKey(x => x.ChecklistTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ChecklistTemplateId, x.Title }).IsUnique();
        });

        modelBuilder.Entity<HrChecklistTemplate>(e =>
            e.HasIndex(x => new { x.CompanyId, x.Kind, x.IsActive }));

        modelBuilder.Entity<HrGrievance>(e =>
        {
            // Two employees are involved and neither may cascade — removing
            // somebody must not delete the complaint they raised or the one
            // raised about them.
            e.HasOne(x => x.Employee).WithMany()
                .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedToEmployee).WithMany()
                .HasForeignKey(x => x.AssignedToEmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CompanyId, x.Status });
        });

        modelBuilder.Entity<HrSkill>(e =>
            e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique());

        modelBuilder.Entity<HrEmployeeSkill>(e =>
            // One entry per person per skill. Two would make "how good are
            // they at rigging" a question with two answers.
            e.HasIndex(x => new { x.EmployeeId, x.SkillId }).IsUnique());

        modelBuilder.Entity<HrExpenseClaimType>(e =>
            e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique());

        modelBuilder.Entity<HrTravelLeg>(e =>
        {
            e.HasOne(x => x.TravelRequest).WithMany(r => r.Legs)
                .HasForeignKey(x => x.TravelRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TravelRequestId);
        });

        modelBuilder.Entity<HrTravelRequest>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.FromDate, x.ToDate }));

        modelBuilder.Entity<HrTimesheetLine>(e =>
        {
            e.HasOne(x => x.Timesheet).WithMany(t => t.Lines)
                .HasForeignKey(x => x.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TimesheetId, x.OnDate });
        });

        modelBuilder.Entity<HrTimesheet>(e =>
            // One timesheet per person per week, or the hours are counted twice.
            e.HasIndex(x => new { x.EmployeeId, x.WeekStarting }).IsUnique());

        modelBuilder.Entity<HrPayslipLine>(e =>
        {
            e.HasOne(x => x.Payslip).WithMany(p => p.Lines)
                .HasForeignKey(x => x.PayslipId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.PayslipId);
        });

        modelBuilder.Entity<HrPayrollRun>(e =>
            e.HasIndex(x => new { x.CompanyId, x.Year, x.Month }).IsUnique());

        modelBuilder.Entity<HrPayslip>(e =>
        {
            // Wired by hand because convention would not have found it. EF looks
            // for a key named after the navigation — RunId — and, not seeing one,
            // invents a shadow column of that name and uses it. PayrollRunId then
            // stays zero on every row written through run.Slips, and every query
            // that filters on PayrollRunId silently returns nothing.
            e.HasOne(x => x.Run).WithMany(r => r.Slips)
                .HasForeignKey(x => x.PayrollRunId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
        });

        modelBuilder.Entity<HrHoliday>(e =>
            e.HasIndex(x => new { x.CompanyId, x.OnDate, x.Name }));
        modelBuilder.Entity<HrPunch>(e =>
            e.HasIndex(x => new { x.EmployeeId, x.At }));
        modelBuilder.Entity<HrGoal>(e =>
            e.HasOne(x => x.Cycle).WithMany().HasForeignKey(x => x.CycleId)
                .OnDelete(DeleteBehavior.SetNull));
        modelBuilder.Entity<HrAppraisal>(e =>
            e.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique());
        modelBuilder.Entity<HrTrainingEnrolment>(e =>
            e.HasIndex(x => new { x.TrainingId, x.EmployeeId }).IsUnique());
        modelBuilder.Entity<HrPolicyAck>(e =>
            e.HasIndex(x => new { x.PolicyId, x.EmployeeId }).IsUnique());

        /* ---------------- statutory payroll ---------------- */
        //
        // Every one of these is read once per employee per payroll run and
        // never written outside setup, so the indexes are the lookup keys a run
        // uses: the date for the rate set, the state for professional tax, the
        // year and regime for the slabs.

        modelBuilder.Entity<HrStatutoryConfig>(e =>
            e.HasIndex(x => new { x.CompanyId, x.EffectiveFrom }));

        modelBuilder.Entity<HrProfessionalTaxSlab>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.State, x.FromAmount });
            e.HasIndex(x => new { x.CompanyId, x.IsActive });
        });

        modelBuilder.Entity<HrIncomeTaxSlab>(e =>
            e.HasIndex(x => new { x.CompanyId, x.FinancialYear, x.Regime, x.SortOrder }));

        modelBuilder.Entity<HrTaxRegimeConfig>(e =>
            e.HasIndex(x => new { x.CompanyId, x.FinancialYear, x.Regime }).IsUnique());

        modelBuilder.Entity<HrEmployeeTaxProfile>(e =>
        {
            // One election per employee per year — a second row would mean two
            // regimes for one year and no way to say which payroll used.
            e.HasIndex(x => new { x.EmployeeId, x.FinancialYear }).IsUnique();

            e.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => new { a.CompanyId, a.At });
            entity.HasIndex(a => new { a.Entity, a.EntityId });

            entity.Property(a => a.Changes).HasColumnType("jsonb");
        });
    }
}
