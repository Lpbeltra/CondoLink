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
        group.MapPost("", CreateAsync); group.MapPut("/{employeeId:guid}", UpdateAsync);
        group.MapPatch("/{employeeId:guid}/status", UpdateStatusAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, Guid? condominiumId, string? status, string? search, string? jobTitle,
        AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var query = db.Employees.AsNoTracking().Where(e => db.Condominiums.Any(c => c.Id == e.CondominiumId && c.ManagementCompanyId == scope.ManagementCompanyId && c.IsActive));
        if (condominiumId.HasValue) query = query.Where(x => x.CondominiumId == condominiumId);
        if (status == "active") query = query.Where(x => x.IsActive); else if (status == "inactive") query = query.Where(x => !x.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) { var q = search.Trim().ToLower(); query = query.Where(x => x.FullName.ToLower().Contains(q) || (x.NormalizedCpf != null && x.NormalizedCpf.Contains(q.Replace(".", "").Replace("-", ""))) || (x.JobTitle != null && x.JobTitle.ToLower().Contains(q))); }
        if (!string.IsNullOrWhiteSpace(jobTitle)) query = query.Where(x => x.JobTitle == jobTitle);
        var rows = await query.OrderBy(x => x.FullName).Select(x => new { x.Id, x.CondominiumId, x.FullName, x.JobTitle, x.PhoneNumber, x.Email, x.RegistrationNumber, x.AdmissionDate, x.IsActive, x.CreatedAt, x.UpdatedAt, Cpf = x.Cpf == null ? null : "***.***.***-" + x.Cpf.Substring(9, 2) }).ToArrayAsync(ct);
        return Results.Ok(rows);
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
        return Results.Ok(new { employee.Id, employee.IsActive, employee.UpdatedAt });
    }

    private static string? MaskCpf(string? cpf) => cpf is null ? null : $"***.***.***-{cpf[^2..]}";
}
