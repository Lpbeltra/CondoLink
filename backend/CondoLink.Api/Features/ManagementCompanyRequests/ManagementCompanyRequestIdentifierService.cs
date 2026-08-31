using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public sealed class ManagementCompanyRequestIdentifierService(AppDbContext db, TimeProvider clock)
{
    public async Task<(string Identifier, DateTime CreatedAt)> NextAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var year = now.Year;
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = db.Database.IsNpgsql()
            ? "INSERT INTO management_company_request_annual_sequences (year, last_value) VALUES (@year, 1) ON CONFLICT (year) DO UPDATE SET last_value = management_company_request_annual_sequences.last_value + 1 RETURNING last_value"
            : "INSERT INTO management_company_request_annual_sequences (year, last_value) VALUES (@year, 1) ON CONFLICT (year) DO UPDATE SET last_value = last_value + 1 RETURNING last_value";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter(); parameter.ParameterName = "year"; parameter.Value = year; command.Parameters.Add(parameter);
        var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        return ($"ADM-{year}-{value:D4}", now);
    }
}
