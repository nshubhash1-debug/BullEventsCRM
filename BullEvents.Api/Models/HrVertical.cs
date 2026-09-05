namespace BullEvents.Api.Models;

/// <summary>Excel company-specific: Realty sites/targets and Seva territories/visits.</summary>
public class HrSiteAllocation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Vertical { get; set; } = "Realty";
    public string Project { get; set; } = string.Empty;
    public string? Site { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? ReportingManager { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrSalesKpi : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Vertical { get; set; } = "Realty";
    public string Period { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal Achievement { get; set; }
    public decimal IncentiveAmount { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingHr;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrTerritoryMap : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Territory { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? Distributor { get; set; }
    public string? Market { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrFieldVisit : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly VisitDate { get; set; }
    public string Place { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Kind { get; set; } = "OD";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrPolicyAck : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PolicyId { get; set; }
    public HrPolicy? Policy { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
}
