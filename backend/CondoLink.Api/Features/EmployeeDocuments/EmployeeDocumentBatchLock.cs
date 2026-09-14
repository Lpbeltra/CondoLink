using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CondoLink.Api.Features.EmployeeDocuments;

internal static class EmployeeDocumentBatchLock
{
    // Same transaction-scoped key for every operation that can delete a payslip
    // or enqueue its outbound. The seed keeps this namespace separate from the
    // condominium/user advisory locks used elsewhere in the application.
    public static async Task<IDbContextTransaction> AcquireAsync(AppDbContext db, Guid batchId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({batchId.ToString()}, 48172));", ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
