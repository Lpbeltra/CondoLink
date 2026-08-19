using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CondoLink.Infrastructure.Persistence;

public sealed record QueryPerformanceSnapshot(int QueryCount, int SlowQueryCount, double TotalDurationMs, double MaximumDurationMs);

public sealed class QueryPerformanceScope
{
    private sealed class State { public int Count; public int Slow; public double Total; public double Max; }
    private readonly AsyncLocal<State?> current = new();
    public IDisposable Begin() { var previous=current.Value; current.Value=new(); return new Restore(()=>current.Value=previous); }
    public void Record(double ms) { var state=current.Value;if(state is null)return;state.Count++;state.Total+=ms;state.Max=Math.Max(state.Max,ms);if(ms>=500)state.Slow++; }
    public QueryPerformanceSnapshot Snapshot(){var s=current.Value;return s is null?new(0,0,0,0):new(s.Count,s.Slow,s.Total,s.Max);}
    private sealed class Restore(Action action):IDisposable{public void Dispose()=>action();}
}

public sealed class QueryPerformanceInterceptor(QueryPerformanceScope scope, ILogger<QueryPerformanceInterceptor> logger) : DbCommandInterceptor
{
    private const double SlowMilliseconds=500;
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result){Record(command,eventData.Duration);return result;}
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken=default){Record(command,eventData.Duration);return ValueTask.FromResult(result);}
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result){Record(command,eventData.Duration);return result;}
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken=default){Record(command,eventData.Duration);return ValueTask.FromResult(result);}
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result){Record(command,eventData.Duration);return result;}
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken=default){Record(command,eventData.Duration);return ValueTask.FromResult(result);}
    private void Record(DbCommand command, TimeSpan duration)
    {
        var ms=duration.TotalMilliseconds;scope.Record(ms);
        if(ms<SlowMilliseconds)return;
        var normalized=Regex.Replace(command.CommandText,@"\s+"," ").Trim();
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..12];
        var operation=normalized.Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant()??"SQL";
        logger.LogWarning("Slow database command. QueryId: {QueryId}; Operation: {Operation}; DurationMs: {DurationMs}; ParameterCount: {ParameterCount}.",hash,operation,Math.Round(ms,1),command.Parameters.Count);
    }
}
