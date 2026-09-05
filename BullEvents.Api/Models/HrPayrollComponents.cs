namespace BullEvents.Api.Models;

/* ====================================================================== *
 * Salary components
 *
 * What existed was Basic, HRA, Allowances, Incentive and Bonus — five
 * columns, which is fine right up until somebody wants a site allowance
 * that is 10% of basic, a night-shift allowance paid only to the crew, or
 * a canteen deduction. Then the choice is a schema change or a lie.
 *
 * A component is a named line on a payslip that knows how to compute
 * itself and what the statute thinks of it. The flags matter more than the
 * arithmetic: whether it counts towards the provident fund wage decides an
 * employer's largest recurring liability, and getting it wrong is not
 * visible until an inspection.
 * ====================================================================== */

public static class SalaryComponentTypes
{
    public const string Earning = "Earning";
    public const string Deduction = "Deduction";

    public static readonly string[] All = [Earning, Deduction];
}

public static class SalaryCalculations
{
    /// <summary>A flat amount, set on the structure.</summary>
    public const string Fixed = "Fixed";

    /// <summary>A percentage of the structure's base figure.</summary>
    public const string PercentOfBase = "PercentOfBase";

    /// <summary>An expression over the other components — "0.1 * BASIC".</summary>
    public const string Formula = "Formula";

    public static readonly string[] All = [Fixed, PercentOfBase, Formula];
}

/// <summary>
/// One line a payslip can carry.
///
/// The statutory components — provident fund, ESI, professional tax, income
/// tax — are marked <see cref="IsStatutory"/> and are never computed from a
/// formula: they come from the payroll engine, which knows the ceilings and
/// the slabs. They exist here only so a payslip can name them in the same
/// list as everything else.
/// </summary>
public class HrSalaryComponent : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short form used in formulas — BASIC, HRA, CONV.</summary>
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>A value from <see cref="SalaryComponentTypes"/>.</summary>
    public string ComponentType { get; set; } = SalaryComponentTypes.Earning;

    /// <summary>A value from <see cref="SalaryCalculations"/>.</summary>
    public string Calculation { get; set; } = SalaryCalculations.Fixed;

    /// <summary>Used when the calculation is a percentage or a formula.</summary>
    public string? Formula { get; set; }

    /* ---------------- what the statute makes of it ---------------- */

    /// <summary>
    /// Whether this counts towards the provident fund wage.
    ///
    /// Basic and dearness allowance always do. Most allowances do not, but the
    /// position on special allowances has moved with the case law, which is why
    /// this is a setting rather than a rule in the code.
    /// </summary>
    public bool AffectsPf { get; set; }

    /// <summary>Whether it counts towards the ESI wage. Nearly everything does.</summary>
    public bool AffectsEsi { get; set; } = true;

    /// <summary>Whether it is taxable salary. Reimbursements usually are not.</summary>
    public bool IsTaxable { get; set; } = true;

    /// <summary>
    /// Whether this is the house rent allowance, for section 10(13A).
    ///
    /// Flagged rather than matched on the name, because a company that calls it
    /// "Accommodation" still gets the exemption.
    /// </summary>
    public bool IsHra { get; set; }

    /// <summary>
    /// Whether loss of pay reduces it.
    ///
    /// Salary does. A fixed reimbursement or a statutory bonus usually does not,
    /// and pro-rating those is how a payslip ends up short.
    /// </summary>
    public bool DependsOnPaymentDays { get; set; } = true;

    /// <summary>
    /// Computed by the payroll engine, not from a formula.
    ///
    /// PF, ESI, professional tax and TDS are here so a payslip can list them
    /// beside the rest; their amounts come from the statutory rules.
    /// </summary>
    public bool IsStatutory { get; set; }

    /// <summary>Where it sits on the payslip. Lower comes first.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// A named set of components — the shape of a salary, without the numbers.
///
/// Separate from the assignment because a hundred people are on the same
/// structure and only the base figure differs. Changing the shape for all of
/// them should be one edit, not a hundred.
/// </summary>
public class HrPayStructure : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Rate for an hour of overtime, when the structure carries one.</summary>
    public decimal OvertimeRate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrPayStructureLine> Lines { get; set; } = new List<HrPayStructureLine>();
}

public class HrPayStructureLine : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int PayStructureId { get; set; }
    public HrPayStructure? PayStructure { get; set; }

    public int SalaryComponentId { get; set; }
    public HrSalaryComponent? SalaryComponent { get; set; }

    /// <summary>Used when the component is a fixed amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Overrides the component's own formula for this structure.</summary>
    public string? Formula { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// One employee on one structure, from a date, on a base figure.
///
/// Effective-dated rather than edited in place, so a payslip reprinted for an
/// earlier month still computes on the structure and base that applied then.
/// </summary>
public class HrPayStructureAssignment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int PayStructureId { get; set; }
    public HrPayStructure? PayStructure { get; set; }

    /// <summary>
    /// The figure percentages and formulas are taken of.
    ///
    /// Usually the monthly cost to company or the monthly gross, depending on
    /// how the structure is written. It is whatever <c>BASE</c> means in the
    /// formulas, and the structure's notes should say which.
    /// </summary>
    public decimal Base { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ====================================================================== *
 * One-off pay
 * ====================================================================== */

/// <summary>
/// Something added to or taken off one month's pay.
///
/// A festival bonus, an arrears payment after a backdated raise, a fine, a
/// recovery. Kept apart from the structure because it is not part of the
/// salary — it happens once, and next month it should be gone.
/// </summary>
public class HrAdditionalSalary : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int SalaryComponentId { get; set; }
    public HrSalaryComponent? SalaryComponent { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The month it lands in.</summary>
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>
    /// Whether it repeats until <see cref="RecurringUntil"/>.
    ///
    /// A recurring addition is the honest way to express a temporary
    /// allowance — a site posting for four months — without pretending it is
    /// part of the salary structure.
    /// </summary>
    public bool IsRecurring { get; set; }
    public DateOnly? RecurringUntil { get; set; }

    /// <summary>Whether loss of pay reduces it. Usually not, for a one-off.</summary>
    public bool DependsOnPaymentDays { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = HrRequestStatuses.Approved;

    /// <summary>Set once it has been carried onto a payslip.</summary>
    public int? PaidInPayrollRunId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Whether this belongs on the payslip for a given month.</summary>
    public bool AppliesTo(int year, int month)
    {
        if (Status != HrRequestStatuses.Approved) return false;

        var asked = new DateOnly(year, month, 1);
        var start = new DateOnly(Year, Month, 1);

        if (!IsRecurring) return Year == year && Month == month;
        return asked >= start && (RecurringUntil is null || asked <= RecurringUntil);
    }
}

/* ====================================================================== *
 * Advances
 * ====================================================================== */

public static class AdvanceStatuses
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Paid = "Paid";
    public const string Recovering = "Recovering";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string WrittenOff = "WrittenOff";

    public static readonly string[] All =
        [Requested, Approved, Paid, Recovering, Closed, Rejected, WrittenOff];

    /// <summary>States in which instalments are still being taken.</summary>
    public static readonly string[] Open = [Paid, Recovering];
}

/// <summary>
/// Money advanced against future salary, recovered in instalments.
///
/// The festival advance is a fixture of an Indian payroll and the reason a
/// deduction appears on a payslip that no salary structure explains. Modelled
/// with its repayments rather than as a running number, because an employee
/// leaving mid-recovery needs a statement of what is still owed, and full and
/// final has to be able to read it.
///
/// Interest-free by design. An interest-bearing employee loan is a perquisite
/// with its own tax treatment, and pretending this handles that would be worse
/// than not offering it.
/// </summary>
public class HrEmployeeAdvance : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public decimal Amount { get; set; }

    /// <summary>How many months it is recovered over.</summary>
    public int Instalments { get; set; } = 1;

    /// <summary>Amount taken each month. The last one absorbs the rounding.</summary>
    public decimal InstalmentAmount { get; set; }

    /// <summary>The first month a deduction is taken.</summary>
    public int RecoveryStartYear { get; set; }
    public int RecoveryStartMonth { get; set; }

    public string? Purpose { get; set; }
    public string Status { get; set; } = AdvanceStatuses.Requested;

    public DateOnly? PaidOn { get; set; }

    /// <summary>Written off rather than recovered, with the reason.</summary>
    public string? WriteOffReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrAdvanceRepayment> Repayments { get; set; } =
        new List<HrAdvanceRepayment>();
}

/// <summary>
/// One instalment taken back, on one payroll run.
///
/// Recorded per run rather than as a decreasing balance so that reprocessing a
/// month cannot double-recover: the run is the key, and a second attempt finds
/// the row already there.
/// </summary>
public class HrAdvanceRepayment : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeAdvanceId { get; set; }
    public HrEmployeeAdvance? EmployeeAdvance { get; set; }

    public int PayrollRunId { get; set; }
    public HrPayrollRun? PayrollRun { get; set; }

    public decimal Amount { get; set; }
    public DateTime RecoveredAt { get; set; } = DateTime.UtcNow;
}

/* ====================================================================== *
 * The payslip, itemised
 * ====================================================================== */

/// <summary>
/// One line of one payslip.
///
/// The payslip's own columns carry the totals the statute needs; these are what
/// those totals are made of. Written at the time the run is processed and never
/// recomputed, so a slip reprinted a year later still shows the components that
/// were paid rather than the ones that exist now.
/// </summary>
public class HrPayslipLine : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int PayslipId { get; set; }
    public HrPayslip? Payslip { get; set; }

    public int? SalaryComponentId { get; set; }
    public HrSalaryComponent? SalaryComponent { get; set; }

    /// <summary>Copied, not looked up — a renamed component must not rewrite history.</summary>
    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>A value from <see cref="SalaryComponentTypes"/>.</summary>
    public string ComponentType { get; set; } = SalaryComponentTypes.Earning;

    public decimal Amount { get; set; }

    /// <summary>Whether the payroll engine produced it rather than a formula.</summary>
    public bool IsStatutory { get; set; }

    public int SortOrder { get; set; }
}
