using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var execute = args.Contains("--execute", StringComparer.Ordinal);
var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
var backupConfirmed = args.Contains("--backup-confirmed", StringComparer.Ordinal);

if (execute == dryRun)
    return Fail("Specify exactly one of --dry-run or --execute.");
if (execute && !backupConfirmed)
    return Fail("--execute requires --backup-confirmed.");

var email = Option("--preserve-user-email")
    ?? Environment.GetEnvironmentVariable("RESET_PRESERVE_USER_EMAIL");
if (string.IsNullOrWhiteSpace(email))
    return Fail("Provide --preserve-user-email or RESET_PRESERVE_USER_EMAIL.");

var connectionString = Option("--connection-string")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    return Fail("Provide --connection-string or ConnectionStrings__DefaultConnection.");

try
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString).Options;
    await using var db = new AppDbContext(options);
    var result = await new ProductionDataResetService(db).RunAsync(email, execute);
    Console.WriteLine(result.Executed ? "RESET EXECUTED" : "DRY RUN — no data changed");
    foreach (var count in result.Counts)
        Console.WriteLine($"{count.Key}: {count.Value}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Reset refused or failed: {exception.Message}");
    return 1;
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("Usage: dotnet run --project backend/CondoLink.DataReset -- (--dry-run | --execute --backup-confirmed) --preserve-user-email EMAIL [--connection-string VALUE]");
    return 2;
}
