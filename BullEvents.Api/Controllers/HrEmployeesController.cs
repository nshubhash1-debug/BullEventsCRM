using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/hr/employees")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrEmployeesController(AppDbContext db) : CrmControllerBase(db)
{
    internal static readonly FieldMap<HrEmployee> Fields = new FieldMap<HrEmployee>()
        .Text("employeeCode", "Employee ID", searchable: true)
        .Text("name", "Name", searchable: true)
        .Text("phone", "Phone", searchable: true)
        .Text("email", "Email", searchable: true)
        .Select("departmentName", "Department", "Department.Name")
        .Select("designationName", "Designation", "Designation.Name")
        .Select("status", "Status")
        .Select("employmentType", "Employment type")
        .Select("collarType", "Team type")
        .Select("location", "Location")
        .Date("joiningDate", "Joining date")
        .Number("departmentId", "Department ID")
        .Date("createdAt", "Created");

    private IQueryable<HrEmployee> Base() => Db.HrEmployees
        .Include(e => e.Department)
        .Include(e => e.Designation)
        .Include(e => e.Manager)
        .Include(e => e.Shift)
        .AsNoTracking();

    internal static HrEmployeeDto ToDto(HrEmployee e) => new(
        e.Id, e.EmployeeCode, e.Name, e.PhotoUrl, e.DateOfBirth, e.Phone, e.Email,
        e.Address, e.EmergencyContactName, e.EmergencyContactPhone,
        e.DepartmentId, e.Department?.Name ?? "—",
        e.DesignationId, e.Designation?.Name ?? "—",
        e.ManagerEmployeeId, e.Manager?.Name,
        e.BranchId, e.Location, e.JoiningDate, e.EmploymentType, e.Status, e.CollarType,
        e.UserId, e.ShiftId, e.Shift?.Name, e.Pan, e.Uan, e.EsicIp, e.CreatedAt);

    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FilterFieldDto>> GetFields() =>
        Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["status"] = Options(EmploymentStatuses.All),
            ["employmentType"] = Options(EmploymentTypes.All),
            ["collarType"] = Options(CollarTypes.All),
        }));

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<HrEmployeeDto>>> Query(
        QueryRequest request, CancellationToken ct)
    {
        return Ok(await RunQueryAsync(
            Base(), request, Fields, ToDto, "Name", false,
            async filtered => new Dictionary<string, decimal>
            {
                ["headcount"] = await filtered.CountAsync(
                    e => EmploymentStatuses.OnRolls.Contains(e.Status), ct),
                ["probation"] = await filtered.CountAsync(
                    e => e.Status == EmploymentStatuses.Probation, ct),
                ["notice"] = await filtered.CountAsync(
                    e => e.Status == EmploymentStatuses.NoticePeriod, ct),
            },
            ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HrEmployeeDto>> Get(int id, CancellationToken ct)
    {
        var row = await Base().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw ApiException.NotFound("Employee");
        return Ok(ToDto(row));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost]
    public async Task<ActionResult<HrEmployeeDto>> Create(HrEmployeeInput input, CancellationToken ct)
    {
        var employee = new HrEmployee { CompanyId = Db.Tenant.CompanyId };
        await ApplyAsync(employee, input, ct);
        employee.OwnerId = input.UserId ?? Db.Tenant.UserId;
        Db.HrEmployees.Add(employee);
        await Db.SaveChangesAsync(ct);
        await SeedOnboardingAsync(employee.Id, ct);
        await SeedLeaveBalancesAsync(employee.Id, ct);
        return Ok(await Reload(employee.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<HrEmployeeDto>> Update(
        int id, HrEmployeeInput input, CancellationToken ct)
    {
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw ApiException.NotFound("Employee");
        await ApplyAsync(employee, input, ct);
        employee.OwnerId = input.UserId ?? employee.OwnerId;
        await Db.SaveChangesAsync(ct);
        return Ok(await Reload(id, ct));
    }

    [HttpGet("{id:int}/documents")]
    public async Task<ActionResult<IReadOnlyList<HrDocumentDto>>> Documents(int id, CancellationToken ct)
    {
        var rows = await Db.HrEmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == id)
            .OrderByDescending(d => d.Id)
            .ToListAsync(ct);
        return Ok(rows.Select(d => new HrDocumentDto(
            d.Id, d.EmployeeId, d.DocumentType, d.FileName, d.Url, d.ExpiryDate, d.ReminderDate)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("{id:int}/documents")]
    public async Task<ActionResult<HrDocumentDto>> AddDocument(
        int id, HrDocumentInput input, CancellationToken ct)
    {
        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw ApiException.NotFound("Employee");
        var row = new HrEmployeeDocument
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = id,
            DocumentType = input.DocumentType.Trim(),
            FileName = input.FileName.Trim(),
            Url = input.Url,
            ExpiryDate = input.ExpiryDate,
            ReminderDate = input.ReminderDate,
        };
        Db.HrEmployeeDocuments.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrDocumentDto(
            row.Id, row.EmployeeId, row.DocumentType, row.FileName, row.Url,
            row.ExpiryDate, row.ReminderDate));
    }

    [HttpGet("{id:int}/onboarding")]
    public async Task<ActionResult<IReadOnlyList<HrOnboardingDto>>> Onboarding(
        int id, CancellationToken ct)
    {
        var rows = await Db.HrOnboardingItems.AsNoTracking()
            .Where(i => i.EmployeeId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(ct);
        return Ok(rows.Select(i => new HrOnboardingDto(i.Id, i.Title, i.Done, i.DoneAt)));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("onboarding/{itemId:int}/toggle")]
    public async Task<ActionResult<HrOnboardingDto>> ToggleOnboarding(int itemId, CancellationToken ct)
    {
        var item = await Db.HrOnboardingItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw ApiException.NotFound("Checklist item");
        item.Done = !item.Done;
        item.DoneAt = item.Done ? DateTime.UtcNow : null;
        await Db.SaveChangesAsync(ct);
        return Ok(new HrOnboardingDto(item.Id, item.Title, item.Done, item.DoneAt));
    }

    private async Task ApplyAsync(HrEmployee e, HrEmployeeInput input, CancellationToken ct)
    {
        var code = input.EmployeeCode.Trim();
        var dup = await Db.HrEmployees.AnyAsync(
            x => x.EmployeeCode == code && x.Id != e.Id, ct);
        if (dup) throw ApiException.Conflict($"Employee ID {code} is already in use.");

        e.EmployeeCode = code;
        e.Name = input.Name.Trim();
        e.DateOfBirth = input.DateOfBirth;
        e.Phone = input.Phone;
        e.Email = input.Email;
        e.Address = input.Address;
        e.EmergencyContactName = input.EmergencyContactName;
        e.EmergencyContactPhone = input.EmergencyContactPhone;
        e.DepartmentId = input.DepartmentId;
        e.DesignationId = input.DesignationId;
        e.ManagerEmployeeId = input.ManagerEmployeeId;
        e.BranchId = input.BranchId;
        e.Location = input.Location;
        e.JoiningDate = DateTime.SpecifyKind(input.JoiningDate.Date, DateTimeKind.Utc);
        e.EmploymentType = Require(input.EmploymentType, EmploymentTypes.All, "employment type");
        e.Status = Require(input.Status, EmploymentStatuses.All, "status");
        e.CollarType = Require(input.CollarType, CollarTypes.All, "collar type");
        e.UserId = input.UserId;
        e.ShiftId = input.ShiftId;
        e.PhotoUrl = input.PhotoUrl;
        e.BankAccount = input.BankAccount;
        e.Ifsc = input.Ifsc;
        e.Pan = input.Pan;
        e.Aadhaar = input.Aadhaar;
        e.Uan = input.Uan;
        e.EsicIp = input.EsicIp;
    }

    private async Task<HrEmployeeDto> Reload(int id, CancellationToken ct)
    {
        var row = await Base().FirstAsync(e => e.Id == id, ct);
        return ToDto(row);
    }

    private async Task SeedOnboardingAsync(int employeeId, CancellationToken ct)
    {
        var titles = new[]
        {
            "Documents collected", "Appointment letter issued", "Employee ID created",
            "Official email", "Assets issued", "Policies acknowledged", "Induction completed",
        };
        foreach (var title in titles)
        {
            Db.HrOnboardingItems.Add(new HrOnboardingItem
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                Title = title,
            });
        }
        await Db.SaveChangesAsync(ct);
    }

    private async Task SeedLeaveBalancesAsync(int employeeId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var types = await Db.HrLeaveTypes.Where(t => t.IsActive).ToListAsync(ct);
        foreach (var type in types)
        {
            Db.HrLeaveBalances.Add(new HrLeaveBalance
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                LeaveTypeId = type.Id,
                Year = year,
                Opening = type.MonthlyEntitlement * 12,
            });
        }
        await Db.SaveChangesAsync(ct);
    }
}
