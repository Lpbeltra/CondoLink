using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class CreateEmployee
{
    public static IEndpointRouteBuilder MapCreateEmployee(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/employees", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("Create a condominium employee");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, EmployeeRequest request,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        if (string.IsNullOrWhiteSpace(request.FullName))
            return Results.BadRequest(new { message = "Nome completo é obrigatório." });

        var normalizedRegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber)
            ? null : request.RegistrationNumber.Trim().ToUpperInvariant();
        if (normalizedRegistrationNumber is not null && await db.Employees.AsNoTracking().AnyAsync(
                x => x.CondominiumId == condominiumId && x.NormalizedRegistrationNumber == normalizedRegistrationNumber, ct))
            return Results.Conflict(new { message = "Já existe um funcionário com esta matrícula neste condomínio." });

        Employee employee;
        try
        {
            employee = new Employee(condominiumId, request.FullName, request.JobTitle, request.PhoneNumber,
                request.Email, request.RegistrationNumber, request.AdmissionDate);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("EmployeeAudit").LogInformation(
            "Employee {Action}. CondominiumId: {CondominiumId}; EmployeeId: {EmployeeId}; ActorUserId: {ActorUserId}; ActorType: {ActorType}; Timestamp: {Timestamp}",
            "Created", condominiumId, employee.Id, actor.UserId, actor.Kind, employee.CreatedAt);

        var response = new EmployeeResponse(employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle,
            employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate,
            employee.IsActive, employee.CreatedAt, employee.UpdatedAt);
        return Results.Created($"/condominiums/{condominiumId}/employees/{employee.Id}", response);
    }
}
