using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class UpdateEmployeeStatus
{
    public static IEndpointRouteBuilder MapUpdateEmployeeStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/condominiums/{condominiumId:guid}/employees/{employeeId:guid}/status", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("Activate or deactivate a condominium employee");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid employeeId, EmployeeStatusRequest request,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.CondominiumId == condominiumId, ct);
        if (employee is null) return Results.NotFound(new { message = "Funcionário não encontrado." });

        if (request.IsActive) employee.Activate();
        else employee.Deactivate();
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("EmployeeAudit").LogInformation(
            "Employee {Action}. CondominiumId: {CondominiumId}; EmployeeId: {EmployeeId}; ActorUserId: {ActorUserId}; ActorType: {ActorType}; Timestamp: {Timestamp}",
            request.IsActive ? "Activated" : "Deactivated", condominiumId, employee.Id, actor.UserId, actor.Kind, employee.UpdatedAt);

        var response = new EmployeeResponse(employee.Id, employee.CondominiumId, employee.FullName, employee.JobTitle,
            employee.PhoneNumber, employee.Email, employee.RegistrationNumber, employee.AdmissionDate,
            employee.IsActive, employee.CreatedAt, employee.UpdatedAt);
        return Results.Ok(response);
    }
}
