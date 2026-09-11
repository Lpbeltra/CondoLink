using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class UpdateEmployee
{
    public static IEndpointRouteBuilder MapUpdateEmployee(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/condominiums/{condominiumId:guid}/employees/{employeeId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("Update a condominium employee");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid employeeId, EmployeeRequest request,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.CondominiumId == condominiumId, ct);
        if (employee is null) return Results.NotFound(new { message = "Funcionário não encontrado." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return Results.BadRequest(new { message = "Nome completo é obrigatório." });

        var normalizedRegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber)
            ? null : request.RegistrationNumber.Trim().ToUpperInvariant();
        if (normalizedRegistrationNumber is not null && await db.Employees.AsNoTracking().AnyAsync(
                x => x.Id != employeeId && x.CondominiumId == condominiumId
                    && x.NormalizedRegistrationNumber == normalizedRegistrationNumber, ct))
            return Results.Conflict(new { message = "Já existe um funcionário com esta matrícula neste condomínio." });

        try
        {
            employee.Update(request.FullName, request.JobTitle, request.PhoneNumber, request.Email,
                request.RegistrationNumber, request.AdmissionDate);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("EmployeeAudit").LogInformation(
            "Employee {Action}. CondominiumId: {CondominiumId}; EmployeeId: {EmployeeId}; ActorUserId: {ActorUserId}; ActorType: {ActorType}; Timestamp: {Timestamp}",
            "Updated", condominiumId, employee.Id, actor.UserId, actor.Kind, employee.UpdatedAt);

        var response = new EmployeeResponse(employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle,
            employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate,
            employee.IsActive, employee.CreatedAt, employee.UpdatedAt);
        return Results.Ok(response);
    }
}
