using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class AdministratorEmployeeEndpoints
{
    public static IEndpointRouteBuilder MapAdministratorEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/administrator/employees").RequireAuthorization().WithTags("EmployeeManagement");
        group.MapGet("", ListAsync); group.MapGet("/condominiums", ListCondominiumsAsync);
        group.MapGet("/{employeeId:guid}", GetAsync);
        group.MapPost("", CreateAsync); group.MapPut("/{employeeId:guid}", UpdateAsync);
        group.MapPatch("/{employeeId:guid}/status", UpdateStatusAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, Guid? condominiumId, string? status, string? search, string? jobTitle, bool? revealCpf,
        AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var query = db.Employees.AsNoTracking().Where(e => db.Condominiums.Any(c => c.Id == e.CondominiumId && c.ManagementCompanyId == scope.ManagementCompanyId && c.IsActive));
        if (condominiumId.HasValue) query = query.Where(x => x.CondominiumId == condominiumId);
        if (status == "active") query = query.Where(x => x.IsActive); else if (status == "inactive") query = query.Where(x => !x.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) { var q = search.Trim().ToLower(); query = query.Where(x => x.FullName.ToLower().Contains(q) || (x.NormalizedCpf != null && x.NormalizedCpf.Contains(q.Replace(".", "").Replace("-", ""))) || (x.JobTitle != null && x.JobTitle.ToLower().Contains(q))); }
        if (!string.IsNullOrWhiteSpace(jobTitle)) query = query.Where(x => x.JobTitle == jobTitle);
        // Bulk reveal stays inside the same authorization boundary as the per-employee
        // detail/edit endpoints below — it is the same administrator seeing the same
        // data they could already fetch one row at a time, never a wider audience.
        var reveal = revealCpf == true;
        var rows = await query.OrderBy(x => x.FullName).Select(x => new { x.Id, x.CondominiumId, x.FullName, x.JobTitle, x.PhoneNumber, x.Email, x.RegistrationNumber, x.AdmissionDate, x.IsActive, x.CreatedAt, x.UpdatedAt,
            Cpf = x.NormalizedCpf == null ? null : (reveal
                ? x.NormalizedCpf.Substring(0, 3) + "." + x.NormalizedCpf.Substring(3, 3) + "." + x.NormalizedCpf.Substring(6, 3) + "-" + x.NormalizedCpf.Substring(9, 2)
                : "***.***.***-" + x.NormalizedCpf.Substring(9, 2)) }).ToArrayAsync(ct);
        return Results.Ok(rows);
    }

    // Full, unmasked CPF is only ever returned here (or by Create/Update, which echo
    // back what the caller just submitted) — the list endpoint's default response
    // stays masked. Used by the edit form so it never has to "unmask" a masked value.
    private static async Task<IResult> GetAsync(Guid employeeId, ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employeeId
            && db.Condominiums.Any(c => c.Id == x.CondominiumId && c.ManagementCompanyId == scope.ManagementCompanyId), ct);
        if (employee is null) return Results.NotFound();
        return Results.Ok(new { employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle, employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate, employee.IsActive, employee.CreatedAt, employee.UpdatedAt, employee.Cpf });
    }

    private static async Task<IResult> CreateAsync(EmployeeRequest request, ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        if (request.CondominiumId is null) return Results.BadRequest(new { message = "Condomínio é obrigatório." });
        if (!await db.Condominiums.AnyAsync(x => x.Id == request.CondominiumId && x.ManagementCompanyId == scope.ManagementCompanyId && x.IsActive, ct)) return Results.StatusCode(403);
        try
        {
            var employee = new Employee(request.CondominiumId.Value, request.FullName, request.Cpf, request.JobTitle, request.PhoneNumber, request.Email, request.RegistrationNumber, request.AdmissionDate);
            db.Employees.Add(employee); await db.SaveChangesAsync(ct);
            return Results.Created($"/administrator/employees/{employee.Id}", new { employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle, employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate, employee.IsActive, employee.CreatedAt, employee.UpdatedAt, Cpf = MaskCpf(employee.Cpf) });
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
    }

    private static async Task<IResult> UpdateAsync(Guid employeeId, EmployeeRequest request, ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && db.Condominiums.Any(c => c.Id == x.CondominiumId && c.ManagementCompanyId == scope.ManagementCompanyId), ct);
        if (employee is null) return Results.NotFound();
        if (request.CondominiumId is null || !await db.Condominiums.AnyAsync(x => x.Id == request.CondominiumId && x.ManagementCompanyId == scope.ManagementCompanyId && x.IsActive, ct)) return Results.StatusCode(403);
        try { employee.MoveToCondominium(request.CondominiumId.Value); employee.Update(request.FullName!, request.Cpf, request.JobTitle, request.PhoneNumber, request.Email, request.RegistrationNumber, request.AdmissionDate); await db.SaveChangesAsync(ct); return Results.Ok(new { employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle, employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate, employee.IsActive, employee.CreatedAt, employee.UpdatedAt, Cpf = MaskCpf(employee.Cpf) }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
    }

    private static async Task<IResult> ListCondominiumsAsync(ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var items = await db.Condominiums.AsNoTracking()
            .Where(x => x.ManagementCompanyId == scope.ManagementCompanyId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToArrayAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> UpdateStatusAsync(Guid employeeId, EmployeeStatusRequest request,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId
            && db.Condominiums.Any(c => c.Id == x.CondominiumId && c.ManagementCompanyId == scope.ManagementCompanyId), ct);
        if (employee is null) return Results.NotFound();
        if (request.IsActive) employee.Activate(); else employee.Deactivate();
        await db.SaveChangesAsync(ct);
        // Regression guard: this must return the FULL employee, not just the changed
        // fields — the frontend replaces its cached item with whatever comes back
        // here, and a partial payload used to blank out the rest of the card (name,
        // job title, tags) the instant a status was toggled.
        return Results.Ok(new { employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle, employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate, employee.IsActive, employee.CreatedAt, employee.UpdatedAt, Cpf = MaskCpf(employee.Cpf) });
    }

    private static string? MaskCpf(string? cpf) => cpf is null ? null : $"***.***.***-{cpf[^2..]}";
}
